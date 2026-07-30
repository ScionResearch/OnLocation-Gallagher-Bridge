using System.Data;
using System.Reflection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using OnLocationGallagherBridge.Data;
using OnLocationGallagherBridge.Models;
using OnLocationGallagherBridge.Services;
using Serilog;

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

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Extensions.Http", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console(restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information)
    .WriteTo.File(Path.Combine(logDir, "bridge-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30, restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Debug)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = contentRoot });
builder.Host.UseWindowsService();
builder.Host.UseSerilog();

builder.WebHost.UseUrls("http://*:5000");

builder.Services.AddSingleton<ConfigService>();
builder.Services.AddDbContext<BridgeDbContext>(options =>
    options.UseSqlite($"Data Source={Path.Combine(dataDir, "bridge.db")}"));

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
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
builder.Services.AddScoped<IConfigurationStatusService, ConfigurationStatusService>();

// The initial match review posts about ten form values per record. The default limit of 1024 is reached
// at roughly a hundred records and the request then fails inside model binding, before any handler or
// logging runs, which presents as a blank page.
builder.Services.Configure<FormOptions>(options => options.ValueCountLimit = 65536);

builder.Services.AddHealthChecks();
builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var cfg = scope.ServiceProvider.GetRequiredService<ConfigService>();
    await cfg.LoadAsync();
    var urls = cfg.GetConfig().WebHost.Urls;
    if (!string.IsNullOrWhiteSpace(urls) && !app.Urls.Contains(urls)) app.Urls.Add(urls);

    var db = scope.ServiceProvider.GetRequiredService<BridgeDbContext>();
    await db.Database.EnsureCreatedAsync();
    await EnsureInitialMatchColumnsAsync(db);
    SeedDefaults(db);
}

app.Run();
Log.CloseAndFlush();

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
            Id = "employees",
            EntityType = "Staff",
            Enabled = false,
            PollingIntervalMinutes = 60,
            OnLocationEndpoint = "staff",
            FieldMapJson = "[{\"source\":\"email\",\"target\":\"email\",\"transform\":\"copy\"},{\"source\":\"first_name\",\"target\":\"firstName\",\"transform\":\"copy\"},{\"source\":\"last_name\",\"target\":\"lastName\",\"transform\":\"copy\"}]",
            AutoCreate = false
        },
        new SyncProfile
        {
            Id = "contractor-members",
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
