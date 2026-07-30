using MailKit.Net.Smtp;
using MimeKit;
using OnLocationGallagherBridge.Models;

namespace OnLocationGallagherBridge.Services;

public interface IAlertService
{
    Task SendAlertAsync(string subject, string body);
    Task<bool> TestAsync(CancellationToken ct = default);
}

public class AlertService : IAlertService
{
    private readonly ConfigService _config;
    private readonly Serilog.ILogger _logger;

    public AlertService(ConfigService config, Serilog.ILogger? logger = null)
    {
        _config = config;
        _logger = logger ?? Serilog.Log.Logger.ForContext<AlertService>();
    }

    public async Task SendAlertAsync(string subject, string body)
    {
        var smtp = _config.GetConfig().Smtp;
        if (!smtp.Enabled || string.IsNullOrEmpty(smtp.Host) || string.IsNullOrWhiteSpace(smtp.From)) return;
        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(smtp.From));
            foreach (var to in smtp.AlertRecipients.Where(r => !string.IsNullOrWhiteSpace(r)))
                message.To.Add(MailboxAddress.Parse(to));
            message.Subject = $"[OnLocation-Gallagher Bridge] {subject}";
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();
            await client.ConnectAsync(smtp.Host, smtp.Port, smtp.EnableSsl ? MailKit.Security.SecureSocketOptions.StartTls : MailKit.Security.SecureSocketOptions.Auto);
            if (!string.IsNullOrEmpty(smtp.Username))
                await client.AuthenticateAsync(smtp.Username, smtp.Password ?? string.Empty);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to send alert");
        }
    }

    public async Task<bool> TestAsync(CancellationToken ct = default)
    {
        try
        {
            await SendAlertAsync("Test alert", "This is a test alert from the OnLocation-Gallagher bridge.");
            return true;
        }
        catch
        {
            return false;
        }
    }
}
