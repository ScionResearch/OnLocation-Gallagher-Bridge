using System.Collections.Concurrent;
using OnLocationGallagherBridge.Models;

namespace OnLocationGallagherBridge.Services;

public interface INotificationService
{
    Task RaiseEventAsync(NotificationEventType eventType, string details, CancellationToken ct = default);
    Task FlushAsync(CancellationToken ct = default);
}

public class NotificationService : INotificationService
{
    private readonly ConfigService _config;
    private readonly IAlertService _alertService;
    private readonly Serilog.ILogger _logger;
    private readonly ConcurrentDictionary<string, GroupState> _states = new();
    private readonly List<DateTimeOffset> _sentTimestamps = new();
    private readonly object _rateLimitLock = new();

    public NotificationService(ConfigService config, IAlertService alertService, Serilog.ILogger? logger = null)
    {
        _config = config;
        _alertService = alertService;
        _logger = logger ?? Serilog.Log.Logger.ForContext<NotificationService>();
    }

    public Task RaiseEventAsync(NotificationEventType eventType, string details, CancellationToken ct = default)
    {
        var cfg = _config.GetConfig().Notifications;
        if (!cfg.Enabled) return Task.CompletedTask;

        var eventName = eventType.ToString();
        var groups = cfg.Groups
            .Where(g => g.EventSources.Contains(eventName, StringComparer.OrdinalIgnoreCase))
            .OrderBy(g => g.Priority)
            .ToList();

        if (!groups.Any()) return Task.CompletedTask;

        var evt = new NotificationEvent(eventType, details, DateTimeOffset.UtcNow);
        _logger.Debug("Raising notification event {EventType} for {GroupCount} group(s)", eventType, groups.Count);

        var tasks = new List<Task>();
        foreach (var group in groups)
        {
            var state = _states.GetOrAdd(group.Name, _ => new GroupState());
            lock (state)
            {
                state.Pending.Add(evt);
                state.CountSinceLastSend++;
            }

            if (group.Mode == "every")
            {
                tasks.Add(TrySendAsync(group, state, ct));
            }
            else if (group.Mode == "grouped" && state.CountSinceLastSend >= group.Threshold)
            {
                tasks.Add(TrySendAsync(group, state, ct));
            }
        }

        return Task.WhenAll(tasks);
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        var cfg = _config.GetConfig().Notifications;
        if (!cfg.Enabled) return;

        var now = DateTimeOffset.UtcNow;

        // Scheduled groups: send if their interval has elapsed and they have pending events.
        foreach (var group in cfg.Groups.OrderBy(g => g.Priority))
        {
            if (group.Mode != "scheduled") continue;
            var state = _states.GetOrAdd(group.Name, _ => new GroupState());
            lock (state)
            {
                if (state.Pending.Count == 0) continue;
                if (state.LastSent.HasValue && now - state.LastSent.Value < ParseInterval(group.Interval)) continue;
            }
            await TrySendAsync(group, state, ct);
        }

        // Drain anything still pending due to rate limiting, highest priority first. Scheduled groups are
        // excluded here - they are only sent once their interval elapses, handled by the loop above -
        // otherwise this would send them immediately on every flush, defeating the schedule entirely.
        foreach (var group in cfg.Groups.OrderBy(g => g.Priority))
        {
            if (group.Mode == "scheduled") continue;
            var state = _states.GetOrAdd(group.Name, _ => new GroupState());
            lock (state)
            {
                if (state.Pending.Count == 0) continue;
            }
            await TrySendAsync(group, state, ct);
            if (!CanSend(cfg.MaxEmailsPerHour)) break;
        }
    }

    private async Task TrySendAsync(NotificationGroup group, GroupState state, CancellationToken ct)
    {
        var cfg = _config.GetConfig().Notifications;
        lock (_rateLimitLock)
        {
            if (!CanSend(cfg.MaxEmailsPerHour))
            {
                _logger.Debug("Notification rate limit reached; holding {Group} until next window", group.Name);
                return;
            }
        }

        List<NotificationEvent> events;
        lock (state)
        {
            if (state.Pending.Count == 0) return;
            events = state.Pending.ToList();
            state.Pending.Clear();
            state.CountSinceLastSend = 0;
        }

        var recipients = ResolveRecipients(group, cfg).ToList();
        if (!recipients.Any())
        {
            _logger.Warning("Notification group {Group} has no recipients; discarding {Count} event(s)", group.Name, events.Count);
            return;
        }

        var subject = BuildSubject(group, events);
        var body = BuildBody(group, events);

        try
        {
            await _alertService.SendAlertAsync(subject, body, ct);
            lock (_rateLimitLock)
            {
                _sentTimestamps.Add(DateTimeOffset.UtcNow);
            }
            lock (state)
            {
                state.LastSent = DateTimeOffset.UtcNow;
            }
            _logger.Information("Sent notification for group {Group} with {Count} event(s) to {Recipients}", group.Name, events.Count, string.Join(", ", recipients));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to send notification for group {Group}", group.Name);
            // Put events back so they can be consolidated in the next window.
            lock (state)
            {
                state.Pending.InsertRange(0, events);
            }
        }
    }

    private bool CanSend(int maxEmailsPerHour)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-1);
        _sentTimestamps.RemoveAll(t => t < cutoff);
        return maxEmailsPerHour <= 0 || _sentTimestamps.Count < maxEmailsPerHour;
    }

    private IEnumerable<string> ResolveRecipients(NotificationGroup group, NotificationConfig cfg)
    {
        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var email in group.RecipientEmails)
        {
            var trimmed = email.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            emails.Add(trimmed);
        }

        // Fall back to legacy SMTP alert recipients if no explicit group recipients are configured.
        if (!emails.Any())
        {
            var smtp = _config.GetConfig().Smtp;
            foreach (var r in smtp.AlertRecipients)
            {
                if (!string.IsNullOrWhiteSpace(r)) emails.Add(r.Trim());
            }
        }

        return emails;
    }

    private static string BuildSubject(NotificationGroup group, List<NotificationEvent> events)
    {
        if (events.Count == 1)
        {
            return $"[OnLocation-Gallagher Bridge] {FormatEventType(events[0].Type)}: {events[0].Details}";
        }
        return $"[OnLocation-Gallagher Bridge] {group.Name}: {events.Count} notification(s)";
    }

    private static string BuildBody(NotificationGroup group, List<NotificationEvent> events)
    {
        var lines = new List<string>
        {
            $"Notification group: {group.Name}",
            $"Mode: {group.Mode}",
            $"Generated at: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            ""
        };

        // The same event (e.g. a record awaiting manual review) is often raised repeatedly across sync
        // cycles until it is resolved. Consolidate identical type+details combinations into a single line
        // with a first/last seen range and occurrence count, rather than repeating one line per attempt.
        var consolidated = events
            .GroupBy(e => (e.Type, e.Details))
            .Select(g => new
            {
                g.Key.Type,
                g.Key.Details,
                First = g.Min(e => e.Timestamp),
                Last = g.Max(e => e.Timestamp),
                Count = g.Count()
            })
            .OrderBy(e => e.First);

        foreach (var evt in consolidated)
        {
            var typeLabel = FormatEventType(evt.Type);
            if (evt.Count == 1)
            {
                lines.Add($"- {evt.First:yyyy-MM-dd HH:mm:ss} UTC [{typeLabel}] {evt.Details}");
            }
            else
            {
                lines.Add($"- {evt.First:yyyy-MM-dd HH:mm:ss} to {evt.Last:yyyy-MM-dd HH:mm:ss} UTC (x{evt.Count}) [{typeLabel}] {evt.Details}");
            }
        }

        if (events.Count > 1)
        {
            lines.Add("");
            lines.Add($"Total events: {events.Count}");
        }

        return string.Join("\r\n", lines);
    }

    private static string FormatEventType(NotificationEventType type) => type switch
    {
        NotificationEventType.ConnectionInterrupted => "Connection interrupted",
        NotificationEventType.ConnectionRestored => "Connection restored",
        NotificationEventType.FailedTransaction => "Failed transaction",
        NotificationEventType.AwaitingUserInput => "Awaiting user input",
        NotificationEventType.ServiceRestarted => "Service restarted",
        _ => type.ToString()
    };

    private static TimeSpan ParseInterval(string interval) => interval?.ToLowerInvariant() switch
    {
        "day" => TimeSpan.FromDays(1),
        "week" => TimeSpan.FromDays(7),
        _ => TimeSpan.FromHours(1)
    };

    private sealed class GroupState
    {
        public List<NotificationEvent> Pending { get; } = new();
        public int CountSinceLastSend { get; set; }
        public DateTimeOffset? LastSent { get; set; }
    }

    private sealed record NotificationEvent(NotificationEventType Type, string Details, DateTimeOffset Timestamp);
}
