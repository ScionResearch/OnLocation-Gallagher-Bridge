using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace OnLocationGallagherBridge.Models;

public class BridgeConfig
{
    public int Version { get; set; } = 1;
    public OnLocationConfig OnLocation { get; set; } = new();
    public GallagherConfig Gallagher { get; set; } = new();
    public SmtpConfig Smtp { get; set; } = new();
    public WebHostConfig WebHost { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
    public NotificationConfig Notifications { get; set; } = new();
}

public class OnLocationConfig
{
    public string BaseUrl { get; set; } = "https://api.whosonlocation.com/v1";
    public string AuthMode { get; set; } = "OAuth2"; // OAuth2 | ApiKey | Basic
    public string? ClientId { get; set; } = "";
    public string? ClientSecret { get; set; } = "";
    public string? ApiKey { get; set; } = "";
    public string? Password { get; set; } = "";
    public string? TokenEndpoint { get; set; } = "https://login.whosonlocation.com/oauth2/token";
    public bool DisableTlsVerification { get; set; } = false;
}

public class GallagherConfig
{
    public string? BaseUrl { get; set; } = "";
    public int Port { get; set; } = 8904;
    public string? ApiKey { get; set; } = "";
    public bool DisableTlsVerification { get; set; } = false;
}

public class SmtpConfig
{
    public string? Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? Username { get; set; } = "";
    public string? Password { get; set; } = "";
    public string? From { get; set; } = "";
    public List<string> AlertRecipients { get; set; } = new();
    public bool Enabled { get; set; } = false;
}

public class WebHostConfig
{
    [BindNever]
    public string? Urls { get; set; } = "https://*:5000";

    public int Port { get; set; } = 5000;
    public HttpsConfig Https { get; set; } = new();
    public AuthConfig Auth { get; set; } = new();
}

public class HttpsConfig
{
    public bool Enabled { get; set; } = true;
    public string CertificateSource { get; set; } = "Auto"; // Auto | Thumbprint | Pfx
    public string? CertificateThumbprint { get; set; }
    public string? CertificatePath { get; set; }
    public string? CertificatePassword { get; set; }
}

public class AuthConfig
{
    public bool Enabled { get; set; } = true;
    public int SessionTimeoutMinutes { get; set; } = 60;
    public int PasswordMinimumLength { get; set; } = 12;
    public bool PasswordRequireUppercase { get; set; } = true;
    public bool PasswordRequireLowercase { get; set; } = true;
    public bool PasswordRequireDigit { get; set; } = true;
    public bool PasswordRequireNonAlphanumeric { get; set; } = true;
    public int MaxFailedLoginAttempts { get; set; } = 5;
    public int LockoutDurationMinutes { get; set; } = 15;

    [BindNever]
    public List<WebUser> Users { get; set; } = new();
}

public class WebUser
{
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public bool IsAdmin { get; set; }
    public bool RequirePasswordChange { get; set; } = true;
    public int FailedLoginAttempts { get; set; }
    public DateTimeOffset? LockoutEndUtc { get; set; }
}

public class LoggingConfig
{
    public string? Path { get; set; } = "";
    public int RetentionDays { get; set; } = 30;
    public string? MinimumLevel { get; set; } = "Information";
}

public class NotificationConfig
{
    public bool Enabled { get; set; }
    public int MaxEmailsPerHour { get; set; } = 60;
    public List<NotificationRecipient> Recipients { get; set; } = new();
    public List<NotificationGroup> Groups { get; set; } = new();
}

public class NotificationRecipient
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}

public class NotificationGroup
{
    public string Name { get; set; } = "";
    public int Priority { get; set; } = 1;
    public List<string> EventSources { get; set; } = new();
    public string Mode { get; set; } = "every"; // every, grouped, scheduled
    public int Threshold { get; set; } = 1;
    public string Interval { get; set; } = "hour"; // hour, day, week
    public List<string> RecipientEmails { get; set; } = new();
}

public enum NotificationEventType
{
    ConnectionInterrupted,
    ConnectionRestored,
    FailedTransaction,
    AwaitingUserInput,
    ServiceRestarted
}
