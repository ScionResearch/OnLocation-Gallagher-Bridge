using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OnLocationGallagherBridge.Data;
using OnLocationGallagherBridge.Models;

namespace OnLocationGallagherBridge.Services;

public class SyncEngine : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SyncEngine> _logger;
    private readonly ISyncActivity _activity;
    private readonly INotificationService _notifications;

    public SyncEngine(IServiceProvider services, ILogger<SyncEngine> logger, ISyncActivity activity, INotificationService notifications)
    {
        _services = services;
        _logger = logger;
        _activity = activity;
        _notifications = notifications;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sync engine started");
        try
        {
            await _notifications.RaiseEventAsync(NotificationEventType.ServiceRestarted, "The OnLocation-Gallagher Bridge sync service has started.", stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to raise service-restarted notification");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunPendingProfilesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync engine loop failed");
            }
            _logger.LogInformation("Sync engine sleeping for 1 minute");
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task RunPendingProfilesAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BridgeDbContext>();
        var source = scope.ServiceProvider.GetRequiredService<IOnLocationSourceService>();
        var processor = scope.ServiceProvider.GetRequiredService<IJobProcessor>();
        var now = DateTimeOffset.UtcNow;

        var allEnabled = await db.SyncProfiles.Where(p => p.Enabled).ToListAsync(ct);
        foreach (var profile in allEnabled)
        {
            var changed = false;
            if (profile.NextRun == null)
            {
                profile.NextRun = now.AddMinutes(Math.Max(1, profile.FastSyncIntervalMinutes));
                changed = true;
            }
            if (profile.NextFullRun == null)
            {
                profile.NextFullRun = ComputeNextFullRun(now, profile);
                changed = true;
            }
            if (changed) await db.SaveChangesAsync(ct);
        }

        var profiles = allEnabled.Where(p => IsDue(p, now)).ToList();
        if (profiles.Count == 0)
        {
            // The loop was otherwise completely silent whenever every profile was disabled or waiting, so a
            // profile that never syncs left no trace in the log to explain why.
            var configured = await db.SyncProfiles.CountAsync(ct);
            var earliest = allEnabled.Count == 0 ? null : allEnabled.Min(p => p.NextRun);
            var earliestText = earliest.HasValue ? earliest.Value.ToString("u") : "(none)";
            _logger.LogInformation("Sync engine has nothing due: {Configured} profile(s) configured, {Enabled} enabled, earliest next run {NextRun}",
                configured, allEnabled.Count, earliestText);
            return;
        }

        _logger.LogInformation("Sync engine found {Due} due profile(s): {Profiles}", profiles.Count, string.Join(", ", profiles.Select(p => p.Id)));

        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
        foreach (var profile in profiles)
        {
            var fullSync = IsFullSyncDue(profile, now);
            await SyncProfileRunner.RunAsync(db, source, processor, audit, _activity, _logger, profile, fullSync, ct);
            profile.LastRun = now;
            profile.NextRun = now.AddMinutes(Math.Max(1, profile.FastSyncIntervalMinutes));
            if (fullSync)
            {
                profile.LastFullRun = now;
                profile.NextFullRun = ComputeNextFullRun(now, profile);
            }
            await db.SaveChangesAsync(ct);
        }
    }

    private static bool IsFastSyncDue(SyncProfile profile, DateTimeOffset now)
    {
        if (profile.NextRun == null || profile.NextRun <= now) return true;
        if (profile.LastRun.HasValue && now >= profile.LastRun.Value.AddMinutes(Math.Max(1, profile.FastSyncIntervalMinutes))) return true;
        return false;
    }

    private static bool IsFullSyncDue(SyncProfile profile, DateTimeOffset now)
    {
        if (profile.NextFullRun.HasValue && profile.NextFullRun.Value <= now) return true;
        return false;
    }

    private static bool IsDue(SyncProfile profile, DateTimeOffset now)
        => IsFastSyncDue(profile, now) || IsFullSyncDue(profile, now);

    public static DateTimeOffset ComputeNextFullRun(DateTimeOffset from, SyncProfile profile)
    {
        var intervalDays = Math.Max(1, profile.FullSyncIntervalDays);
        var timeOfDay = TimeSpan.FromMinutes(Math.Clamp(profile.FullSyncTimeOfDayMinutes, 0, 1439));
        var local = from.ToLocalTime();

        // Use the date of the last full run if we have one so changing the interval
        // immediately changes the next scheduled run. Otherwise fall back to today.
        var baseDate = profile.LastFullRun?.ToLocalTime().Date ?? local.Date;
        var candidate = baseDate.Add(timeOfDay);

        var minimum = local;
        if (profile.LastFullRun.HasValue && profile.LastFullRun.Value.ToLocalTime() > minimum)
            minimum = profile.LastFullRun.Value.ToLocalTime();

        while (candidate <= minimum) candidate = candidate.AddDays(intervalDays);
        return new DateTimeOffset(candidate, local.Offset);
    }
}
