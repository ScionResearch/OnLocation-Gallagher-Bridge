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

        foreach (var profile in profiles)
        {
            var fullSync = IsFullSyncDue(profile, now);
            await RunProfileAsync(db, source, processor, profile, fullSync, ct);
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

    private async Task RunProfileAsync(BridgeDbContext db, IOnLocationSourceService source, IJobProcessor processor, SyncProfile profile, bool fullSync, CancellationToken ct)
    {
        if (!profile.InitialMatchCompleted)
        {
            _logger.LogInformation("Profile {Profile} is blocked pending initial record match", profile.Id);
            return;
        }

        var mode = fullSync ? "full" : "fast";
        var correlation = Guid.NewGuid().ToString("N");
        _logger.LogInformation("Running profile {Profile} ({Mode} sync) with correlation {Correlation}", profile.Id, mode, correlation);
        _activity.Begin(profile.Id, $"Starting ({mode} sync)");

        var bookmark = await db.SyncBookmarks.FindAsync(new object?[] { profile.Id }, cancellationToken: ct);
        var newBookmark = bookmark == null;
        bookmark ??= new SyncBookmark { ProfileId = profile.Id };

        IReadOnlyList<JsonElement> records;
        try
        {
            records = await source.GetRecordsAsync(profile, bookmark, fullSync, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Profile {Profile} fetch failed", profile.Id);
            _activity.Complete($"{profile.Id}: fetch failed \u2014 {ex.Message}");
            return;
        }

        _activity.SetPhase("Queueing", $"{records.Count} record(s) to write to Command Centre");
        foreach (var record in records)
        {
            var id = GetId(record);
            if (string.IsNullOrEmpty(id)) continue;

            var payload = record.ToString() ?? "{}";
            var existing = await db.SyncJobs.FirstOrDefaultAsync(j => j.ProfileId == profile.Id && j.SourceId == id && j.Status == "Pending", ct);
            if (existing != null)
            {
                existing.PayloadJson = payload;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                var completedJobs = await db.SyncJobs
                    .Where(j => j.ProfileId == profile.Id && j.SourceId == id && j.Status == "Complete")
                    .ToListAsync(ct);
                var latestCompleted = completedJobs.OrderByDescending(j => j.UpdatedAt).FirstOrDefault();
                if (latestCompleted != null && latestCompleted.PayloadJson == payload) continue;

                db.SyncJobs.Add(new SyncJob
                {
                    ProfileId = profile.Id,
                    SourceType = profile.EntityType,
                    SourceId = id,
                    PayloadJson = payload,
                    Status = "Pending",
                    CorrelationId = correlation
                });
            }
        }
        await db.SaveChangesAsync(ct);

        var pending = await db.SyncJobs.Where(j => j.ProfileId == profile.Id && j.Status == "Pending").ToListAsync(ct);
        var processed = 0;
        foreach (var job in pending)
        {
            job.Status = "Running";
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            _activity.SetProgress(processed, pending.Count, $"Writing {job.SourceId} to Command Centre");
            await processor.ProcessAsync(job, correlation, ct);
            processed++;
            _activity.SetProgress(processed, pending.Count);
        }

        if (newBookmark)
            db.SyncBookmarks.Add(bookmark);
        else
            db.SyncBookmarks.Update(bookmark);
        await db.SaveChangesAsync(ct);

        var snapshot = _activity.Snapshot;
        _activity.Complete($"{profile.Id}: checked {snapshot.RecordsChecked} induction record(s), wrote {processed} cardholder update(s)");
    }

    private async Task<IReadOnlyList<JsonElement>> FetchInductionHoldersAsync(IOnLocationConnector onLocation, SyncProfile profile, SyncBookmark bookmark, CancellationToken ct)
    {
        var parts = profile.OnLocationEndpoint.Split('/').LastOrDefault()?.Split('?').FirstOrDefault();
        if (string.IsNullOrEmpty(parts) || !int.TryParse(parts, out var inductionId)) parts = "1";
        // Use the endpoint segment as induction id
        var id = profile.OnLocationEndpoint.Trim('/').Split('/').LastOrDefault() ?? "1";
        return await onLocation.GetInductionHoldersAsync(id, bookmark, ct);
    }

    private static string? GetId(JsonElement record)
    {
        if (record.TryGetProperty("id", out var id)) return id.ValueKind == JsonValueKind.String ? id.GetString() : id.GetRawText();
        return null;
    }
}
