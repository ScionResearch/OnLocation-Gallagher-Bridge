using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using OnLocationGallagherBridge.Data;
using OnLocationGallagherBridge.Models;
using OnLocationGallagherBridge.Services;
using Serilog;
using Serilog.Core;
using Serilog.Events;

var assemblyLocation = Assembly.GetExecutingAssembly().Location;
var appRoot = Path.GetDirectoryName(assemblyLocation) ?? AppContext.BaseDirectory;
var contentRoot = appRoot;
while (contentRoot is not null && !Directory.Exists(Path.Combine(contentRoot, "wwwroot")))
    contentRoot = Directory.GetParent(contentRoot)?.FullName;
if (contentRoot is null) contentRoot = appRoot;

var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
var dataDir = Path.Combine(programData, "OnLocation-Gallagher-Bridge");
var logDir = Path.Combine(dataDir, "logs");
Directory.CreateDirectory(dataDir);
Directory.CreateDirectory(logDir);

var configPath = Path.Combine(dataDir, "config");
Directory.CreateDirectory(configPath);

// Config must be loaded before the logger is built so the persisted minimum log level takes effect
// immediately on startup, rather than always starting in Debug (which writes full staff PII to disk).
var configService = new ConfigService();
await configService.LoadAsync();

// Break-glass local recovery: lets an operator with console/RDP access to the host reset a web login
// password without going through the (possibly locked-out) web UI. Intentionally bypasses the web host
// entirely so it works even if the Windows Service won't start.
if (args.Length > 0 && string.Equals(args[0], "--reset-password", StringComparison.OrdinalIgnoreCase))
{
    await RunResetPasswordAsync(configService, args.ElementAtOrDefault(1));
    return;
}

var loggingConfig = configService.GetConfig().Logging;
var levelSwitch = new LoggingLevelSwitch(ParseLogLevel(loggingConfig.MinimumLevel));
var retainedDays = loggingConfig.RetentionDays > 0 ? loggingConfig.RetentionDays : 30;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.ControlledBy(levelSwitch)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Extensions.Http", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
    .WriteTo.File(Path.Combine(logDir, "bridge-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: retainedDays)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRoot
});

builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "OnLocationGallagherBridge";
});

builder.Host.UseSerilog();

// Validate HTTPS configuration before Kestrel tries to bind. If HTTPS is enabled but we cannot
// load a usable certificate, revert the WebHost configuration to the safe defaults (HTTP on port 5000)
// so the UI remains accessible and the admin can fix the certificate from the Settings page.
var appConfig = configService.GetConfig();
var webHostConfig = appConfig.WebHost;
var httpsEnabled = webHostConfig.Https.Enabled;
X509Certificate2? httpsCert = null;

if (httpsEnabled)
{
    httpsCert = CertificateLoader.Load(webHostConfig.Https, dataDir, configService, Log.Logger);
    if (httpsCert is null)
    {
        Log.Logger.Error("HTTPS is enabled but the configured certificate could not be loaded. Switching to an auto-generated certificate.");
        webHostConfig.Https.CertificateSource = "Auto";
        webHostConfig.Https.CertificateThumbprint = null;
        webHostConfig.Https.CertificatePath = null;

        httpsCert = CertificateLoader.Load(webHostConfig.Https, dataDir, configService, Log.Logger);
        if (httpsCert is null)
        {
            Log.Logger.Error("Auto-generated certificate could not be loaded. Reverting to HTTP on default port 5000.");
            webHostConfig.Https.Enabled = false;
            webHostConfig.Port = 5000;
            configService.SaveAsync(appConfig).GetAwaiter().GetResult();
            httpsEnabled = false;
        }
    }
}

var scheme = httpsEnabled ? "https" : "http";
var listenUrl = $"{scheme}://*:{webHostConfig.Port}";
builder.WebHost.UseUrls(listenUrl);

builder.WebHost.ConfigureKestrel((context, options) =>
{
    if (httpsEnabled && httpsCert is not null)
    {
        options.ConfigureHttpsDefaults(listenOptions => listenOptions.ServerCertificate = httpsCert);
    }
});

builder.Services.AddSingleton(configService);
builder.Services.AddSingleton(levelSwitch);
builder.Services.AddSingleton<IApplicationSessionService, ApplicationSessionService>();
builder.Services.AddDbContext<BridgeDbContext>(options =>
    options.UseSqlite($"Data Source={Path.Combine(dataDir, "bridge.db")}"));

builder.Services.AddSingleton<IWebAuthService, WebAuthService>();

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("OnLocation")
    .ConfigurePrimaryHttpMessageHandler(sp =>
    {
        var config = sp.GetRequiredService<ConfigService>();
        return CreateIpv4Handler(() => config.GetConfig().OnLocation.DisableTlsVerification);
    });
builder.Services.AddHttpClient("Gallagher")
    .ConfigurePrimaryHttpMessageHandler(sp =>
    {
        var config = sp.GetRequiredService<ConfigService>();
        return CreateIpv4Handler(() => config.GetConfig().Gallagher.DisableTlsVerification);
    });
builder.Services.AddSingleton<ISyncActivity, SyncActivityService>();
builder.Services.AddSingleton<IOnLocationConnector, OnLocationConnector>();
builder.Services.AddSingleton<IOnLocationSourceService, OnLocationSourceService>();
builder.Services.AddSingleton<IGallagherConnector, GallagherConnector>();
builder.Services.AddSingleton<IIdentityMatcher, IdentityMatcher>();
builder.Services.AddSingleton<ITransformEngine, TransformEngine>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IJobProcessor, JobProcessor>();
builder.Services.AddHostedService<SyncEngine>();
builder.Services.AddSingleton<IAlertService, AlertService>();
builder.Services.AddSingleton<INotificationService, NotificationService>();
builder.Services.AddHostedService<NotificationScheduler>();
builder.Services.AddHostedService<ConnectionMonitorService>();
builder.Services.AddHostedService<StatusFileService>();
builder.Services.AddScoped<IConfigurationStatusService, ConfigurationStatusService>();

// The initial match review posts about ten form values per record. The default limit of 1024 is reached
// at roughly a hundred records and the request then fails inside model binding, before any handler or
// logging runs, which presents as a blank page.
builder.Services.Configure<FormOptions>(options => options.ValueCountLimit = 65536);

var authEnabled = webHostConfig.Auth.Enabled;
if (authEnabled)
{
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Login";
            options.LogoutPath = "/Logout";
            options.AccessDeniedPath = "/AccessDenied";
            options.Cookie.Name = "OnLocationGallagherBridge.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            // When HTTPS is off the cookie must not be marked Secure, otherwise browsers on remote hosts
            // (unlike localhost/127.0.0.1) will refuse to send it over plain HTTP.
            options.Cookie.SecurePolicy = webHostConfig.Https.Enabled ? CookieSecurePolicy.Always : CookieSecurePolicy.None;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(webHostConfig.Auth.SessionTimeoutMinutes);
            options.Events.OnValidatePrincipal = async context =>
            {
                var sessionService = context.HttpContext.RequestServices.GetRequiredService<IApplicationSessionService>();
                var token = context.Principal?.FindFirst("SessionToken")?.Value;
                if (token != sessionService.SessionToken)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                // The SessionToken check above only catches a full app restart. Re-check the user record on
                // every request so a disabled or deleted account is logged out immediately, rather than
                // staying valid until the cookie's sliding expiration lapses.
                var username = context.Principal?.Identity?.Name;
                var webAuth = context.HttpContext.RequestServices.GetRequiredService<IWebAuthService>();
                var user = string.IsNullOrEmpty(username) ? null : webAuth.FindUser(username);
                if (user is null || !user.IsEnabled)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                // Keep role/admin/password-change claims in sync with the current user record so that
                // permission changes (e.g. an admin demoting a user) take effect without requiring re-login.
                var identity = (ClaimsIdentity)context.Principal!.Identity!;
                var isAdminClaim = identity.HasClaim(ClaimTypes.Role, "Admin");
                var requiresChangeClaim = identity.HasClaim("RequirePasswordChange", "true");
                if (isAdminClaim != user.IsAdmin || requiresChangeClaim != user.RequirePasswordChange)
                {
                    var newIdentity = webAuth.CreateClaimsIdentity(user);
                    context.ReplacePrincipal(new ClaimsPrincipal(newIdentity));
                    context.ShouldRenew = true;
                }
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });
}

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")))
    .SetApplicationName("OnLocationGallagherBridge");

builder.Services.AddHealthChecks();
builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

if (webHostConfig.Https.Enabled)
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();

if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();

    app.Use(async (context, next) =>
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.HasClaim("RequirePasswordChange", "true") &&
            !context.Request.Path.StartsWithSegments("/ChangePassword") &&
            !context.Request.Path.StartsWithSegments("/Logout") &&
            !context.Request.Path.StartsWithSegments("/Error"))
        {
            context.Response.Redirect("/ChangePassword");
            return;
        }
        await next();
    });
}

app.MapHealthChecks("/health").AllowAnonymous();
app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var cfg = scope.ServiceProvider.GetRequiredService<ConfigService>();
    var webAuth = scope.ServiceProvider.GetRequiredService<IWebAuthService>();
    var db = scope.ServiceProvider.GetRequiredService<BridgeDbContext>();
    await db.Database.EnsureCreatedAsync();
    await EnsureInitialMatchColumnsAsync(db);
    SeedDefaults(db);
    SeedDefaultAdmin(cfg, webAuth);
}

app.Run();
Log.CloseAndFlush();

static LogEventLevel ParseLogLevel(string? value) =>
    Enum.TryParse<LogEventLevel>(value, ignoreCase: true, out var level) ? level : LogEventLevel.Information;

static async Task RunResetPasswordAsync(ConfigService configService, string? username)
{
    var cfg = configService.GetConfig();
    if (!cfg.WebHost.Auth.Users.Any())
    {
        Console.WriteLine("No local users are configured yet. Start the service normally; a default admin account will be created automatically.");
        Environment.Exit(1);
        return;
    }

    var user = string.IsNullOrWhiteSpace(username)
        ? null
        : cfg.WebHost.Auth.Users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

    if (user is null)
    {
        Console.WriteLine(string.IsNullOrWhiteSpace(username)
            ? "Usage: OnLocationGallagherBridge.exe --reset-password <username>"
            : $"No user named '{username}' was found.");
        Console.WriteLine("Available users:");
        foreach (var u in cfg.WebHost.Auth.Users)
            Console.WriteLine($"  {u.Username}{(u.IsAdmin ? " (admin)" : "")}{(u.IsEnabled ? "" : " (disabled)")}");
        Environment.Exit(1);
        return;
    }

    Console.WriteLine("WARNING: Stop the OnLocationGallagherBridge Windows Service first (net stop OnLocationGallagherBridge).");
    Console.WriteLine("Resetting a password while the service is running risks the service overwriting this change when it next saves its configuration.");
    Console.WriteLine();
    Console.WriteLine($"Resetting password for user '{user.Username}'...");

    var webAuth = new WebAuthService(configService, new ApplicationSessionService());
    var newPassword = GenerateRandomPassword(cfg.WebHost.Auth);
    webAuth.SetPassword(user, newPassword);
    user.RequirePasswordChange = true;
    user.IsEnabled = true;
    user.FailedLoginAttempts = 0;
    user.LockoutEndUtc = null;

    try
    {
        await configService.SaveAsync(cfg);
    }
    catch (UnauthorizedAccessException)
    {
        // The config file lives under %ProgramData%, which the Windows Service writes to as SYSTEM.
        // A normal (non-elevated) command prompt runs with a filtered admin token that can read it but
        // not write it, even for a local administrator, so this needs an elevated prompt.
        Console.WriteLine();
        Console.WriteLine("Access denied writing the configuration file.");
        Console.WriteLine("Re-run this command from an elevated command prompt (right-click Command Prompt, 'Run as administrator').");
        Environment.Exit(1);
        return;
    }

    Console.WriteLine();
    Console.WriteLine("Password reset. Temporary password (you will be required to change it on next login):");
    Console.WriteLine();
    Console.WriteLine($"    {newPassword}");
    Console.WriteLine();
    Environment.Exit(0);
}

static string GenerateRandomPassword(AuthConfig rules)
{
    const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    const string lower = "abcdefghijkmnopqrstuvwxyz";
    const string digits = "23456789";
    const string special = "!@#$%^&*-_=+";

    var pools = new List<string>();
    if (rules.PasswordRequireUppercase) pools.Add(upper);
    if (rules.PasswordRequireLowercase) pools.Add(lower);
    if (rules.PasswordRequireDigit) pools.Add(digits);
    if (rules.PasswordRequireNonAlphanumeric) pools.Add(special);
    if (pools.Count == 0) pools.Add(upper + lower + digits);

    var length = Math.Max(16, rules.PasswordMinimumLength);
    var all = string.Concat(pools);
    var chars = new List<char>();
    foreach (var pool in pools)
        chars.Add(pool[RandomNumberGenerator.GetInt32(pool.Length)]);
    while (chars.Count < length)
        chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);

    // Shuffle so the guaranteed-category characters aren't always at the start.
    for (var i = chars.Count - 1; i > 0; i--)
    {
        var j = RandomNumberGenerator.GetInt32(i + 1);
        (chars[i], chars[j]) = (chars[j], chars[i]);
    }

    return new string(chars.ToArray());
}

static async Task EnsureInitialMatchColumnsAsync(BridgeDbContext db)
{
    await EnsureColumnAsync(db, "SyncProfiles", "InitialMatchCompleted", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnAsync(db, "SyncProfiles", "InitialMatchCompletedAt", "TEXT NULL");
    await EnsureColumnAsync(db, "EntityMappings", "Excluded", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnAsync(db, "SyncProfiles", "SelectedInductionIdsJson", "TEXT NOT NULL DEFAULT '[]'");
    await EnsureColumnAsync(db, "SyncProfiles", "DefaultDivisionHref", "TEXT NOT NULL DEFAULT ''");
    await EnsureColumnAsync(db, "SyncProfiles", "DefaultDivisionName", "TEXT NOT NULL DEFAULT ''");
    await EnsureColumnAsync(db, "SyncProfiles", "DefaultAccessGroupsJson", "TEXT NOT NULL DEFAULT '[]'");
    await EnsureColumnAsync(db, "AuditLogs", "SourceDisplay", "TEXT NULL");
    await EnsureColumnAsync(db, "AuditLogs", "Error", "TEXT NULL");
    await EnsureColumnAsync(db, "AuditLogs", "Outcome", "TEXT NOT NULL DEFAULT 'Success'");
    await EnsureColumnAsync(db, "AuditLogs", "DurationMs", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnAsync(db, "SyncBookmarks", "InductionCursorsJson", "TEXT NOT NULL DEFAULT '{}'");
    await EnsureColumnAsync(db, "SyncProfiles", "SyncWindowDays", "INTEGER NOT NULL DEFAULT 7");
    await EnsureColumnAsync(db, "SyncProfiles", "FastSyncIntervalMinutes", "INTEGER NOT NULL DEFAULT 5");
    await EnsureColumnAsync(db, "SyncProfiles", "FullSyncIntervalDays", "INTEGER NOT NULL DEFAULT 1");
    await EnsureColumnAsync(db, "SyncProfiles", "FullSyncTimeOfDayMinutes", "INTEGER NOT NULL DEFAULT 60");
    await EnsureColumnAsync(db, "SyncProfiles", "FullSyncLookbackMonths", "INTEGER NULL");
    await EnsureColumnAsync(db, "SyncProfiles", "FastSyncRecordCount", "INTEGER NOT NULL DEFAULT 10");
    await EnsureColumnAsync(db, "SyncProfiles", "LastRun", "TEXT NULL");
    await EnsureColumnAsync(db, "SyncProfiles", "LastFullRun", "TEXT NULL");
    await EnsureColumnAsync(db, "SyncProfiles", "NextFullRun", "TEXT NULL");
    await EnsureColumnAsync(db, "SyncProfiles", "BridgeMessageTarget", "TEXT NOT NULL DEFAULT ''");
    await EnsureColumnAsync(db, "SyncProfiles", "DefaultUnmatchedAction", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnAsync(db, "ManualMatchQueues", "UpdatedAt", "TEXT NOT NULL DEFAULT ''");
}

static async Task EnsureColumnAsync(BridgeDbContext db, string tableName, string columnName, string columnDefinition)
{
    var connection = db.Database.GetDbConnection();
    if (connection.State != ConnectionState.Open) await connection.OpenAsync();

    var exists = false;
    await using (var command = connection.CreateCommand())
    {
        command.CommandText = $"PRAGMA table_info({tableName})";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                break;
            }
        }
    }

    if (!exists)
    {
        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}";
        await alterCommand.ExecuteNonQueryAsync();
    }
}

static void SeedDefaults(BridgeDbContext db)
{
    if (db.SyncProfiles.Any()) return;
    db.SyncProfiles.AddRange(
        new SyncProfile
        {
            Id = "Staff",
            EntityType = "Staff",
            Enabled = false,
            PollingIntervalMinutes = 60,
            OnLocationEndpoint = "staff",
            FieldMapJson = "[{\"source\":\"email\",\"target\":\"email\",\"transform\":\"copy\"},{\"source\":\"first_name\",\"target\":\"firstName\",\"transform\":\"copy\"},{\"source\":\"last_name\",\"target\":\"lastName\",\"transform\":\"copy\"}]",
            AutoCreate = false
        },
        new SyncProfile
        {
            Id = "Contractors",
            EntityType = "SpMember",
            Enabled = false,
            PollingIntervalMinutes = 60,
            OnLocationEndpoint = "sp/member",
            FieldMapJson = "[{\"source\":\"email\",\"target\":\"email\",\"transform\":\"copy\"},{\"source\":\"first_name\",\"target\":\"firstName\",\"transform\":\"copy\"},{\"source\":\"last_name\",\"target\":\"lastName\",\"transform\":\"copy\"}]",
            AutoCreate = false
        }
    );
    db.SaveChanges();
}

static void SeedDefaultAdmin(ConfigService config, IWebAuthService webAuth)
{
    var cfg = config.GetConfig();
    if (!cfg.WebHost.Auth.Enabled || cfg.WebHost.Auth.Users.Any()) return;

    var admin = new WebUser
    {
        Username = "admin",
        IsAdmin = true,
        IsEnabled = true,
        RequirePasswordChange = true
    };
    webAuth.SetPassword(admin, "admin");
    cfg.WebHost.Auth.Users.Add(admin);
    config.SaveAsync().GetAwaiter().GetResult();
    Log.Logger.Warning("Created default admin user (username: admin, password: admin). You must change this password on first login.");
}

static SocketsHttpHandler CreateIpv4Handler(Func<bool> isTlsVerificationDisabled)
{
    var handler = new SocketsHttpHandler();
    handler.ConnectCallback = async (context, ct) =>
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
        var endpoint = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                       ?? addresses.FirstOrDefault()
                       ?? throw new InvalidOperationException($"Could not resolve {context.DnsEndPoint.Host}");
        Log.Debug("Resolved {Host} to {Addresses}; connecting to {Endpoint}:{Port}",
            context.DnsEndPoint.Host, addresses, endpoint, context.DnsEndPoint.Port);

        var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(endpoint, context.DnsEndPoint.Port, ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch (SocketException ex)
        {
            Log.Error(ex, "Connection to {Endpoint}:{Port} ({Family}) refused for {Host}",
                endpoint, context.DnsEndPoint.Port, endpoint.AddressFamily, context.DnsEndPoint.Host);
            socket.Dispose();
            throw;
        }
    };

    handler.SslOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, errors) =>
    {
        var disabled = isTlsVerificationDisabled();
        if (disabled)
        {
            Log.Debug("TLS verification disabled; accepting certificate with errors {Errors}", errors);
            return true;
        }
        if (errors == System.Net.Security.SslPolicyErrors.None)
        {
            return true;
        }
        Log.Debug("TLS verification failed with {Errors}; rejecting certificate", errors);
        return false;
    };

    return handler;
}
