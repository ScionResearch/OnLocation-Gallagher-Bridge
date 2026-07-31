using MailKit.Net.Smtp;
using MimeKit;
using OnLocationGallagherBridge.Models;

namespace OnLocationGallagherBridge.Services;

public interface IAlertService
{
    Task SendAlertAsync(string subject, string body, CancellationToken ct = default);
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

    public async Task SendAlertAsync(string subject, string body, CancellationToken ct = default)
    {
        var smtp = _config.GetConfig().Smtp;
        if (!smtp.Enabled || string.IsNullOrEmpty(smtp.Host) || string.IsNullOrWhiteSpace(smtp.From)) return;
        try
        {
            var recipients = smtp.AlertRecipients
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .ToList();

            if (!recipients.Any())
            {
                recipients = _config.GetConfig().Notifications.Recipients
                    .Where(r => !string.IsNullOrWhiteSpace(r.Email))
                    .Select(r => r.Email.Trim())
                    .ToList();
            }

            if (!recipients.Any())
            {
                _logger.Warning("Cannot send alert: no recipients have been configured");
                throw new InvalidOperationException("No recipients have been specified.");
            }

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(smtp.From));
            foreach (var to in recipients)
                message.To.Add(MailboxAddress.Parse(to));
            message.Subject = $"[OnLocation-Gallagher Bridge] {subject}";
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();
            await client.ConnectAsync(smtp.Host, smtp.Port, smtp.EnableSsl ? MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable : MailKit.Security.SecureSocketOptions.Auto, ct);
            if (!string.IsNullOrEmpty(smtp.Username))
                await client.AuthenticateAsync(smtp.Username, smtp.Password ?? string.Empty, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to send alert");
            throw;
        }
    }

    public async Task<bool> TestAsync(CancellationToken ct = default)
    {
        var smtp = _config.GetConfig().Smtp;
        if (!smtp.Enabled || string.IsNullOrEmpty(smtp.Host) || string.IsNullOrWhiteSpace(smtp.From))
            return false;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            await SendAlertAsync("Test alert", "This is a test alert from the OnLocation-Gallagher bridge.", cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
