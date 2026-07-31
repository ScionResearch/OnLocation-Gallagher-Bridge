using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OnLocationGallagherBridge.Models;
using OnLocationGallagherBridge.Services;

namespace OnLocationGallagherBridge.Pages;

public class NotificationsModel : PageModel
{
    private readonly ConfigService _config;
    private readonly IAlertService _alertService;

    public NotificationsModel(ConfigService config, IAlertService alertService)
    {
        _config = config;
        _alertService = alertService;
    }

    [BindProperty]
    public SmtpConfig Smtp { get; set; } = new();

    [BindProperty]
    public NotificationConfig Notifications { get; set; } = new();

    public string? Message { get; set; }

    public IReadOnlyList<(string Value, string Display)> EventTypes { get; } = new List<(string, string)>
    {
        ("ConnectionInterrupted", "Connection interrupted"),
        ("ConnectionRestored", "Connection restored"),
        ("FailedTransaction", "Failed transaction"),
        ("AwaitingUserInput", "Awaiting user input"),
        ("ServiceRestarted", "Service restarted")
    };

    public void OnGet()
    {
        var cfg = _config.GetConfig();
        Smtp = cfg.Smtp;
        Notifications = cfg.Notifications;
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        var cfg = _config.GetConfig();
        Smtp.Enabled = Notifications.Enabled;
        cfg.Smtp = Smtp;
        cfg.Notifications = Notifications;
        await _config.SaveAsync(cfg);
        Message = "Notification settings saved.";
        return Page();
    }

    public async Task<IActionResult> OnPostTestAsync()
    {
        var ok = await _alertService.TestAsync();
        Message = ok ? "Test email sent. Check the recipient inbox." : "Test email failed. Check the SMTP settings and logs.";
        return Page();
    }
}
