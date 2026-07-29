using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OnLocationGallagherBridge.Data;
using OnLocationGallagherBridge.Models;

namespace OnLocationGallagherBridge.Services;

public class SyncEngine : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SyncEngine> _logger;

    public SyncEngine(IServiceProvider services, ILogger<SyncEngine> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sync engine started");
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
        var profiles = allEnabled.Where(p => p.NextRun == null || p.NextRun <= now).ToList();
        if (profiles.Count == 0)
        {
            // The loop was otherwise completely silent whenever every profile was disabled or waiting, so a
            // profile that never syncs left no trace in the log to explain why.
            var configured = await db.SyncProfiles.CountAsync(ct);
            _logger.LogDebug("Sync engine has nothing due: {Configured} profile(s) configured, {Enabled} enabled, earliest next run {NextRun}",
                configured, allEnabled.Count, allEnabled.Count == 0 ? null : allEnabled.Min(p => p.NextRun));
        }

        foreach (var profile in profiles)
        {
            await RunProfileAsync(db, source, processor, profile, ct);
            profile.LastRun = now;
            profile.NextRun = now.AddMinutes(profile.PollingIntervalMinutes);
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task RunProfileAsync(BridgeDbContext db, IOnLocationSourceService source, IJobProcessor processor, SyncProfile profile, CancellationToken ct)
    {
        if (!profile.InitialMatchCompleted)
        {
            _logger.LogInformation("Profile {Profile} is blocked pending initial record match", profile.Id);
            return;
        }

        var correlation = Guid.NewGuid().ToString("N");
        _logger.LogInformation("Running profile {Profile} with correlation {Correlation}", profile.Id, correlation);

        var bookmark = await db.SyncBookmarks.FindAsync(new object?[] { profile.Id }, cancellationToken: ct);
        var newBookmark = bookmark == null;
        bookmark ??= new SyncBookmark { ProfileId = profile.Id };

        IReadOnlyList<JsonElement> records;
        try
        {
            records = await source.GetRecordsAsync(profile, bookmark, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Profile {Profile} fetch failed", profile.Id);
            return;
        }

        foreach (var record in records)
        {
            var id = GetId(record);
            if (string.IsNullOrEmpty(id)) continue;

            var existing = await db.SyncJobs.FirstOrDefaultAsync(j => j.ProfileId == profile.Id && j.SourceId == id && j.Status == "Pending", ct);
            if (existing != null)
            {
                existing.PayloadJson = record.ToString() ?? "{}";
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                db.SyncJobs.Add(new SyncJob
                {
                    ProfileId = profile.Id,
                    SourceType = profile.EntityType,
                    SourceId = id,
                    PayloadJson = record.ToString() ?? "{}",
                    Status = "Pending",
                    CorrelationId = correlation
                });
            }
        }
        await db.SaveChangesAsync(ct);

        var pending = await db.SyncJobs.Where(j => j.ProfileId == profile.Id && j.Status == "Pending").ToListAsync(ct);
        foreach (var job in pending)
        {
            job.Status = "Running";
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            await processor.ProcessAsync(job, correlation, ct);
        }

        if (newBookmark)
            db.SyncBookmarks.Add(bookmark);
        else
            db.SyncBookmarks.Update(bookmark);
        await db.SaveChangesAsync(ct);
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
