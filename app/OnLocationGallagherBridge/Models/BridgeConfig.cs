using System.Text.Json.Serialization;

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
}

public class GallagherConfig
{
    public string? BaseUrl { get; set; } = "";
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
    public string? Urls { get; set; } = "http://*:5000";
    public string? AdminPasswordHash { get; set; } = "";
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
