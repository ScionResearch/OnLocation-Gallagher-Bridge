using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OnLocationGallagherBridge.Data;
using OnLocationGallagherBridge.Models;

namespace OnLocationGallagherBridge.Services;

public record RunProfileSummary(
    int RecordsChecked,
    int JobsCreated,
    int Changed,
    int Failed,
    string Message);

public static class SyncProfileRunner
{
    public static async Task<RunProfileSummary> RunAsync(
        BridgeDbContext db,
        IOnLocationSourceService source,
        IJobProcessor processor,
        IAuditService audit,
        ISyncActivity activity,
        ILogger logger,
        SyncProfile profile,
        bool fullSync,
        CancellationToken ct)
    {
        await activity.WaitForRunAsync(ct);
        try
        {
            return await RunAsyncCore(db, source, processor, audit, activity, logger, profile, fullSync, ct);
        }
        finally
        {
            activity.ReleaseRunLock();
        }
    }

    private static async Task<RunProfileSummary> RunAsyncCore(
        BridgeDbContext db,
        IOnLocationSourceService source,
        IJobProcessor processor,
        IAuditService audit,
        ISyncActivity activity,
        ILogger logger,
        SyncProfile profile,
        bool fullSync,
        CancellationToken ct)
    {
        var mode = fullSync ? "full" : "fast";
        var correlation = Guid.NewGuid().ToString("N");
        logger.LogInformation("Running profile {Profile} ({Mode} sync) with correlation {Correlation}", profile.Id, mode, correlation);
        activity.Begin(profile.Id, $"Starting ({mode} sync)");
        var profileStopwatch = System.Diagnostics.Stopwatch.StartNew();

        var bookmark = await db.SyncBookmarks.FindAsync(new object?[] { profile.Id }, cancellationToken: ct);
        var newBookmark = bookmark == null;
        bookmark ??= new SyncBookmark { ProfileId = profile.Id };

        async Task<RunProfileSummary> Complete(string outcome, string detail, int checkedCount = 0, int changedCount = 0, int failedCount = 0, int createdCount = 0)
        {
            var durationMs = (int)profileStopwatch.ElapsedMilliseconds;
            var verb = outcome == "Success" ? "completed" : "failed";
            var summaryMessage = $"{(fullSync ? "Full" : "Fast")} sync {verb} at {DateTimeOffset.UtcNow:u}. {detail} Duration {SyncActivitySnapshot.FormatDuration(TimeSpan.FromMilliseconds(durationMs))}.";
            await audit.LogAsync(new AuditEntry
            {
                CorrelationId = correlation,
                ProfileId = profile.Id,
                SourceId = profile.Id,
                SourceDisplay = profile.Id,
                Action = "SyncSummary",
                Outcome = outcome,
                Message = summaryMessage,
                DurationMs = durationMs
            });
            activity.Complete($"{profile.Id}: {detail}");
            return new RunProfileSummary(checkedCount, createdCount, changedCount, failedCount, detail);
        }

        if (!profile.InitialMatchCompleted)
        {
            logger.LogInformation("Profile {Profile} is blocked pending initial record match", profile.Id);
            return await Complete("Failed", $"Profile '{profile.Id}' is blocked pending initial record match.");
        }

        IReadOnlyList<JsonElement> records;
        try
        {
            records = await source.GetRecordsAsync(profile, bookmark, fullSync, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Profile {Profile} fetch failed", profile.Id);
            return await Complete("Failed", $"Fetch failed \u2014 {ex.Message}");
        }

        activity.SetPhase("Queueing", $"{records.Count} record(s) to write to Command Centre");

        try
        {
            var created = 0;
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
                    if (latestCompleted != null && PayloadsEqual(latestCompleted.PayloadJson, payload)) continue;

                    db.SyncJobs.Add(new SyncJob
                    {
                        ProfileId = profile.Id,
                        SourceType = profile.EntityType,
                        SourceId = id,
                        PayloadJson = payload,
                        Status = "Pending",
                        CorrelationId = correlation
                    });
                    created++;
                }
            }
            await db.SaveChangesAsync(ct);

            activity.AddMatched(created);

            var pending = await db.SyncJobs.Where(j => j.ProfileId == profile.Id && j.Status == "Pending").ToListAsync(ct);
            activity.SetProgress(0, pending.Count, $"Writing {pending.Count} record(s) to Command Centre");
            var processed = 0;
            var results = new List<JobProcessResult>();
            foreach (var job in pending)
            {
                job.Status = "Running";
                job.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                activity.SetProgress(processed, pending.Count, $"Writing {job.SourceId} to Command Centre");
                results.Add(await processor.ProcessAsync(job, correlation, ct));
                processed++;
                activity.SetProgress(processed, pending.Count);
            }

            if (newBookmark)
                db.SyncBookmarks.Add(bookmark);
            else
                db.SyncBookmarks.Update(bookmark);
            await db.SaveChangesAsync(ct);

            var checkedCount = records.Count;
            var changedCount = results.Count(r => r.Changed);
            var failedCount = results.Count(r => r.Failed);
            var unchangedCount = checkedCount - changedCount;
            var outcome = failedCount == 0 ? "Success" : "Failed";
            var detail = checkedCount == 0
                ? "No records returned from OnLocation."
                : $"Checked {checkedCount} OnLocation record(s), {changedCount} needed updating, {unchangedCount} unchanged, {failedCount} failed.";
            logger.LogInformation("Profile {Profile} sync {Outcome}: {Detail}", profile.Id, outcome, detail);
            return await Complete(outcome, detail, checkedCount, changedCount, failedCount, created);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Profile {Profile} sync failed during processing", profile.Id);
            return await Complete("Failed", $"Sync failed during processing \u2014 {ex.Message}", records.Count, 0, 0, 0);
        }
    }

    private static string? GetId(JsonElement record)
    {
        if (record.TryGetProperty("id", out var id)) return id.ValueKind == JsonValueKind.String ? id.GetString() : id.GetRawText();
        return null;
    }

    private static bool PayloadsEqual(string? a, string? b)
    {
        if (string.Equals(a, b, StringComparison.Ordinal)) return true;
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        try
        {
            using var docA = JsonDocument.Parse(a!);
            using var docB = JsonDocument.Parse(b!);
            return JsonEquals(docA.RootElement, docB.RootElement);
        }
        catch
        {
            return false;
        }
    }

    private static bool JsonEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind) return false;

        switch (a.ValueKind)
        {
            case JsonValueKind.String:
                return string.Equals(a.GetString(), b.GetString(), StringComparison.Ordinal);
            case JsonValueKind.Number:
                return a.GetDecimal() == b.GetDecimal();
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                return true;
            case JsonValueKind.Array:
                var aArray = a.EnumerateArray().ToList();
                var bArray = b.EnumerateArray().ToList();
                if (aArray.Count != bArray.Count) return false;
                for (var i = 0; i < aArray.Count; i++)
                    if (!JsonEquals(aArray[i], bArray[i])) return false;
                return true;
            case JsonValueKind.Object:
                var aProps = a.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);
                var bProps = b.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);
                if (aProps.Count != bProps.Count) return false;
                foreach (var prop in aProps)
                {
                    if (!bProps.TryGetValue(prop.Key, out var bValue)) return false;
                    if (!JsonEquals(prop.Value, bValue)) return false;
                }
                return true;
            default:
                return a.GetRawText() == b.GetRawText();
        }
    }
}
