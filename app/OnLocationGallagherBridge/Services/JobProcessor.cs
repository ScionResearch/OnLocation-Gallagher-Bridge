using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OnLocationGallagherBridge.Data;
using OnLocationGallagherBridge.Models;

namespace OnLocationGallagherBridge.Services;

public record JobProcessResult(string Action, string Outcome, bool Changed, bool Failed);

public interface IJobProcessor
{
    Task<JobProcessResult> ProcessAsync(SyncJob job, string correlationId, CancellationToken ct = default);
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

    public async Task<JobProcessResult> ProcessAsync(SyncJob job, string correlationId, CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var profile = await _db.SyncProfiles.FindAsync(new object?[] { job.ProfileId }, cancellationToken: ct);
        if (profile == null) return new JobProcessResult("Unknown", "Failed", false, true);

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
            return new JobProcessResult("Excluded", "Success", false, false);
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
                    _logger.Information("Profile {Profile} source {Source} ({Display}) ignored by default", profile.Id, entityId, display);
                    return new JobProcessResult("Ignored", "Success", false, false);
            }
        }

        if (mapping == null || !mapping.ManualOverride)
        {
            await QueueManualMatchAsync(profile, job, entityId, mapping, ct);
            job.Status = "ManualReview";
            job.Error = null;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.Information("Profile {Profile} source {Source} ({Display}) queued for manual match", profile.Id, entityId, display);
            try
            {
                await _notifications.RaiseEventAsync(NotificationEventType.AwaitingUserInput, $"Record {entityId} ({display}) in profile {profile.Id} is waiting for manual review.", ct);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to raise awaiting-user-input notification");
            }
            return new JobProcessResult("ManualReview", "Pending", false, false);
        }

        var payload = await _transform.BuildCardholderPayloadAsync(transformed, mapping.GallagherHref, ct);
        var bridgeMessageField = TransformEngine.ToGallagherFieldName(profile.BridgeMessageTarget ?? string.Empty);

        if (!string.IsNullOrEmpty(mapping.GallagherHref) &&
            await IsCardholderUnchangedAsync(mapping.GallagherHref, payload, bridgeMessageField, ct))
        {
            job.Status = "Complete";
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.Information("Profile {Profile} source {Source} ({Display}) already in sync with Gallagher; skipping update", profile.Id, entityId, display);
            return new JobProcessResult("NoChange", "Success", false, false);
        }

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
                    try
                    {
                        await _notifications.RaiseEventAsync(NotificationEventType.AwaitingUserInput, $"Record {entityId} ({display}) in profile {profile.Id} lost its link to Command Centre and is waiting for manual review.", ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to raise awaiting-user-input notification for stale link");
                    }
                    return new JobProcessResult("StaleLink", "Failed", true, true);
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
                return new JobProcessResult("Update", "Failed", true, true);
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
                return new JobProcessResult("Create", "Failed", true, true);
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
        return new JobProcessResult(action, "Success", true, false);
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

    private async Task<bool> IsCardholderUnchangedAsync(string href, Dictionary<string, object?> payload, string? excludedField, CancellationToken ct)
    {
        if (payload.Count == 0) return true;

        var expand = new List<string>();
        if (payload.Any(kvp => kvp.Key.StartsWith("@", StringComparison.OrdinalIgnoreCase))) expand.Add("personalDataFields");
        if (payload.ContainsKey("competencies")) expand.Add("competencies");

        try
        {
            var current = await _gallagher.GetCardholderAsync(href, expand.Count > 0 ? string.Join(",", expand) : null, ct);
            if (!current.HasValue) return false;

            foreach (var kvp in payload)
            {
                if (string.Equals(kvp.Key, excludedField, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(kvp.Key, "competencies", StringComparison.OrdinalIgnoreCase)) return false;

                var currentValue = GetCardholderProperty(current.Value, kvp.Key);
                if (!ValuesEqual(currentValue, kvp.Value)) return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to fetch existing cardholder for no-change comparison; proceeding with update");
            return false;
        }
    }

    private static JsonElement? GetCardholderProperty(JsonElement cardholder, string key)
    {
        if (cardholder.ValueKind != JsonValueKind.Object) return null;

        if (key.StartsWith("@", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var property in cardholder.EnumerateObject())
            {
                if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase))
                    return property.Value;
            }
            return null;
        }

        if (cardholder.TryGetProperty(key, out var value)) return value;
        return null;
    }

    private static bool ValuesEqual(JsonElement? current, object? desired)
    {
        var desiredElement = ToJsonElement(desired);
        if (current is null && desiredElement.ValueKind == JsonValueKind.Null) return true;
        if (current is null || desiredElement.ValueKind == JsonValueKind.Null) return false;
        return JsonEquals(current.Value, desiredElement);
    }

    private static JsonElement ToJsonElement(object? value)
    {
        if (value is null) return JsonDocument.Parse("null").RootElement.Clone();
        if (value is JsonElement element) return element.Clone();
        var json = JsonSerializer.Serialize(value);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
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
                {
                    if (!JsonEquals(aArray[i], bArray[i])) return false;
                }
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

internal static class DictionaryExtensions
{
    public static TValue? GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key) where TKey : notnull
    {
        return dict.TryGetValue(key, out var value) ? value : default;
    }
}
