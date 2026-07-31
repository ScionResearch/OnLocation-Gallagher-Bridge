using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OnLocationGallagherBridge.Data;
using OnLocationGallagherBridge.Models;

namespace OnLocationGallagherBridge.Services;

public interface IJobProcessor
{
    Task ProcessAsync(SyncJob job, string correlationId, CancellationToken ct = default);
}

public class JobProcessor : IJobProcessor
{
    private readonly BridgeDbContext _db;
    private readonly IGallagherConnector _gallagher;
    private readonly ITransformEngine _transform;
    private readonly IAuditService _audit;
    private readonly INotificationService _notifications;
    private readonly Serilog.ILogger _logger;

    public JobProcessor(BridgeDbContext db, IGallagherConnector gallagher, ITransformEngine transform, IAuditService audit, INotificationService notifications, Serilog.ILogger? logger = null)
    {
        _db = db;
        _gallagher = gallagher;
        _transform = transform;
        _audit = audit;
        _notifications = notifications;
        _logger = logger ?? Serilog.Log.Logger.ForContext<JobProcessor>();
    }

    public async Task ProcessAsync(SyncJob job, string correlationId, CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var profile = await _db.SyncProfiles.FindAsync(new object?[] { job.ProfileId }, cancellationToken: ct);
        if (profile == null) return;

        using var sourceDoc = JsonDocument.Parse(job.PayloadJson);
        var source = sourceDoc.RootElement;
        var transformed = _transform.Transform(profile, source);

        var entityId = GetString(source, "id");
        var display = DescribeSource(source, entityId);
        var mapping = await _db.EntityMappings.FirstOrDefaultAsync(m => m.ProfileId == profile.Id && m.SourceType == profile.EntityType && m.SourceId == entityId, ct);

        if (mapping?.Excluded == true)
        {
            job.Status = "Complete";
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync(new AuditEntry
            {
                CorrelationId = correlationId,
                ProfileId = profile.Id,
                SourceId = entityId ?? "",
                SourceDisplay = display,
                Action = "Excluded",
                Message = "Excluded during the initial record match, so nothing was sent to Gallagher.",
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            });
            return;
        }

        if (mapping == null)
        {
            switch (profile.DefaultUnmatchedAction)
            {
                case UnmatchedAction.CreateNew:
                    mapping = new EntityMapping
                    {
                        ProfileId = profile.Id,
                        SourceType = profile.EntityType,
                        SourceId = entityId ?? "",
                        GallagherHref = string.Empty,
                        ManualOverride = true
                    };
                    _db.EntityMappings.Add(mapping);
                    break;
                case UnmatchedAction.Ignore:
                    _db.EntityMappings.Add(new EntityMapping
                    {
                        ProfileId = profile.Id,
                        SourceType = profile.EntityType,
                        SourceId = entityId ?? "",
                        GallagherHref = string.Empty,
                        Excluded = true
                    });
                    job.Status = "Complete";
                    job.UpdatedAt = DateTimeOffset.UtcNow;
                    await _db.SaveChangesAsync(ct);
                    await _audit.LogAsync(new AuditEntry
                    {
                        CorrelationId = correlationId,
                        ProfileId = profile.Id,
                        SourceId = entityId ?? "",
                        SourceDisplay = display,
                        Action = "Ignored",
                        Outcome = "Success",
                        AfterJson = job.PayloadJson,
                        Message = "No Gallagher cardholder matched this OnLocation record and the profile default is Ignore.",
                        DurationMs = (int)stopwatch.ElapsedMilliseconds
                    });
                    _logger.Information("Profile {Profile} source {Source} ({Display}) ignored by default", profile.Id, entityId, display);
                    return;
            }
        }

        if (mapping == null || !mapping.ManualOverride)
        {
            await QueueManualMatchAsync(profile, job, entityId, mapping, ct);
            job.Status = "ManualReview";
            job.Error = null;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync(new AuditEntry
            {
                CorrelationId = correlationId,
                ProfileId = profile.Id,
                SourceId = entityId ?? "",
                SourceDisplay = display,
                Action = "ManualReview",
                Outcome = "Pending",
                AfterJson = job.PayloadJson,
                Message = mapping == null
                    ? "No Gallagher cardholder is linked to this OnLocation record. Waiting for an operator to match or create one."
                    : "The existing link has not been confirmed by an operator. Waiting for a decision on the Match Review page.",
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            });
            _logger.Information("Profile {Profile} source {Source} ({Display}) queued for manual match", profile.Id, entityId, display);
            try
            {
                await _notifications.RaiseEventAsync(NotificationEventType.AwaitingUserInput, $"Record {entityId} ({display}) in profile {profile.Id} is waiting for manual review.", ct);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to raise awaiting-user-input notification");
            }
            return;
        }

        var payload = await _transform.BuildCardholderPayloadAsync(transformed, mapping.GallagherHref, ct);
        ApplyBridgeMessage(profile, payload, string.IsNullOrEmpty(mapping.GallagherHref) ? "Created" : "Updated");
        var payloadJson = JsonSerializer.Serialize(payload);

        string? href;
        string action;
        var before = mapping.GallagherHref;
        if (!string.IsNullOrEmpty(mapping.GallagherHref))
        {
            href = mapping.GallagherHref;
            var result = await _gallagher.UpdateCardholderAsync(href, payload, ct);
            if (!result.Success)
            {
                // A 404 means the link itself is broken rather than the data being wrong, so retrying the same
                // request forever cannot help. The link is dropped and the record goes back for re-matching.
                if (result.NotFound)
                {
                    mapping.GallagherHref = string.Empty;
                    mapping.GallagherId = null;
                    mapping.ManualOverride = false;
                    mapping.UpdatedAt = DateTimeOffset.UtcNow;
                    await QueueManualMatchAsync(profile, job, entityId, null, ct);
                    job.Status = "ManualReview";
                    job.Error = result.Error;
                    job.UpdatedAt = DateTimeOffset.UtcNow;
                    await _db.SaveChangesAsync(ct);
                    await _audit.LogAsync(new AuditEntry
                    {
                        CorrelationId = correlationId,
                        ProfileId = profile.Id,
                        SourceId = entityId ?? "",
                        SourceDisplay = display,
                        Action = "StaleLink",
                        Outcome = "Failed",
                        GallagherHref = before,
                        BeforeJson = before,
                        Error = result.Error,
                        Message = "The linked cardholder no longer exists in Command Centre. The link was removed and the record sent back for matching.",
                        DurationMs = (int)stopwatch.ElapsedMilliseconds
                    });
                    _logger.Warning("Profile {Profile} source {Source} ({Display}) had a stale link to {Href}", profile.Id, entityId, display, before);
                    return;
                }

                job.Status = "Failed";
                job.Error = result.Error;
                job.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
                await _audit.LogAsync(new AuditEntry
                {
                    CorrelationId = correlationId,
                    ProfileId = profile.Id,
                    SourceId = entityId ?? "",
                    SourceDisplay = display,
                    Action = "Update",
                    Outcome = "Failed",
                    GallagherHref = href,
                    AfterJson = payloadJson,
                    Error = result.Error,
                    Message = $"Command Centre rejected the update with status {result.StatusCode}.",
                    DurationMs = (int)stopwatch.ElapsedMilliseconds
                });
                _logger.Warning("Gallagher update failed for {Source} ({Display}): {Error}", entityId, display, result.Error);
                try
                {
                    await _notifications.RaiseEventAsync(NotificationEventType.FailedTransaction, $"Update failed for {entityId} ({display}) in profile {profile.Id}: {result.Error}", ct);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to raise failed-transaction notification");
                }
                return;
            }

            action = "Update";
        }
        else
        {
            var created = await _gallagher.CreateCardholderAsync(_transform.ApplyCreateDefaults(profile, payload), ct);
            if (!created.HasValue)
            {
                var error = await _gallagher.GetLastErrorAsync();
                job.Status = "Failed";
                job.Error = error;
                job.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
                await _audit.LogAsync(new AuditEntry
                {
                    CorrelationId = correlationId,
                    ProfileId = profile.Id,
                    SourceId = entityId ?? "",
                    SourceDisplay = display,
                    Action = "Create",
                    Outcome = "Failed",
                    AfterJson = payloadJson,
                    Error = error,
                    Message = "Command Centre rejected the new cardholder.",
                    DurationMs = (int)stopwatch.ElapsedMilliseconds
                });
                _logger.Warning("Gallagher create failed for {Source} ({Display}): {Error}", entityId, display, error);
                try
                {
                    await _notifications.RaiseEventAsync(NotificationEventType.FailedTransaction, $"Create failed for {entityId} ({display}) in profile {profile.Id}: {error}", ct);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to raise failed-transaction notification");
                }
                return;
            }

            href = GetString(created.Value, "href");
            mapping.GallagherHref = href ?? "";
            mapping.GallagherId = GetString(created.Value, "id");
            mapping.UpdatedAt = DateTimeOffset.UtcNow;
            action = "Create";
        }

        await _db.SaveChangesAsync(ct);
        job.Status = "Complete";
        job.Error = null;
        job.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEntry
        {
            CorrelationId = correlationId,
            ProfileId = profile.Id,
            SourceId = entityId ?? "",
            SourceDisplay = display,
            Action = action,
            Outcome = "Success",
            BeforeJson = string.IsNullOrEmpty(before) ? null : before,
            AfterJson = payloadJson,
            GallagherHref = href,
            Message = Summarise(action, payload, profile.BridgeMessageTarget),
            DurationMs = (int)stopwatch.ElapsedMilliseconds
        });
        _logger.Information("Profile {Profile} source {Source} ({Display}) {Action} href {Href}", profile.Id, entityId, display, action, href);
    }

    private static void ApplyBridgeMessage(SyncProfile profile, Dictionary<string, object?> payload, string action)
    {
        var target = profile.BridgeMessageTarget?.Trim();
        if (string.IsNullOrWhiteSpace(target)) return;

        var fieldName = TransformEngine.ToGallagherFieldName(target);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        payload[fieldName] = $"{action} by OnLocation Bridge - {timestamp}";
    }

    // Every poll used to add another queue row for the same person, which is how a handful of unmatched records
    // turned into a queue of dozens.
    private async Task QueueManualMatchAsync(SyncProfile profile, SyncJob job, string? entityId, EntityMapping? mapping, CancellationToken ct)
    {
        var existing = await _db.ManualMatchQueues
            .FirstOrDefaultAsync(m => m.ProfileId == profile.Id && m.SourceId == entityId && m.Status == "Pending", ct);
        if (existing != null)
        {
            existing.SourceJson = job.PayloadJson;
            existing.CandidateHref = mapping?.GallagherHref;
            existing.Confidence = mapping?.Confidence ?? 0;
        }
        else
        {
            _db.ManualMatchQueues.Add(new ManualMatchQueue
            {
                ProfileId = profile.Id,
                SourceId = entityId ?? "",
                SourceJson = job.PayloadJson,
                CandidateHref = mapping?.GallagherHref,
                Confidence = mapping?.Confidence ?? 0,
                Status = "Pending"
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    private static string Summarise(string action, Dictionary<string, object?> payload, string? bridgeMessageTarget)
    {
        var excludedTarget = TransformEngine.ToGallagherFieldName(bridgeMessageTarget ?? string.Empty);
        var fields = payload.Keys.Where(k =>
            !k.Equals("description", StringComparison.OrdinalIgnoreCase)
            && !k.Equals(excludedTarget, StringComparison.OrdinalIgnoreCase)).ToList();
        var competencies = payload.ContainsKey("competencies") ? " including competencies" : string.Empty;
        var verb = action == "Create" ? "Created cardholder with" : "Updated";
        return $"{verb} {fields.Count} field(s){competencies}: {string.Join(", ", fields.Take(12))}";
    }

    private static string DescribeSource(JsonElement source, string? entityId)
    {
        var name = GetString(source, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            var first = GetString(source, "first_name");
            var last = GetString(source, "last_name");
            name = $"{first} {last}".Trim();
        }
        if (string.IsNullOrWhiteSpace(name)) name = GetString(source, "email");
        return string.IsNullOrWhiteSpace(name) ? entityId ?? "(unknown)" : name;
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.String) return prop.GetString();
                if (prop.ValueKind == JsonValueKind.Number) return prop.GetRawText();
                if (prop.ValueKind == JsonValueKind.True) return "true";
                if (prop.ValueKind == JsonValueKind.False) return "false";
            }
        }
        return null;
    }
}

internal static class DictionaryExtensions
{
    public static TValue? GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key) where TKey : notnull
    {
        return dict.TryGetValue(key, out var value) ? value : default;
    }
}
