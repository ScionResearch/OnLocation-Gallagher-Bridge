using System.Text.Json;
using System.Text.Json.Nodes;
using OnLocationGallagherBridge.Models;

namespace OnLocationGallagherBridge.Services;

public interface IOnLocationSourceService
{
    Task<IReadOnlyList<JsonElement>> GetRecordsAsync(SyncProfile profile, SyncBookmark bookmark, CancellationToken ct = default);
    Task<IReadOnlyList<JsonElement>> GetInitialMatchRecordsAsync(SyncProfile profile, DateTimeOffset completedSince, CancellationToken ct = default);
}

public class OnLocationSourceService : IOnLocationSourceService
{
    private readonly IOnLocationConnector _onLocation;
    private readonly Serilog.ILogger _logger;

    public OnLocationSourceService(IOnLocationConnector onLocation, Serilog.ILogger? logger = null)
    {
        _onLocation = onLocation;
        _logger = logger ?? Serilog.Log.Logger.ForContext<OnLocationSourceService>();
    }

    public async Task<IReadOnlyList<JsonElement>> GetRecordsAsync(SyncProfile profile, SyncBookmark bookmark, CancellationToken ct = default)
    {
        var people = profile.EntityType switch
        {
            "Staff" => await _onLocation.GetStaffAsync(bookmark, ct),
            "SpMember" => await _onLocation.GetContractorMembersAsync(bookmark, ct),
            _ => Array.Empty<JsonElement>()
        };

        var inductionIds = GetSelectedInductionIds(profile);
        if (inductionIds.Count == 0) return people;

        var personIdField = GetPersonIdField(profile);
        var attemptsByInduction = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase);
        foreach (var inductionId in inductionIds)
        {
            var attempts = await _onLocation.GetInductionHoldersAsync(inductionId, new SyncBookmark { ProfileId = profile.Id }, ct);
            attemptsByInduction[inductionId] = LatestAttemptPerPerson(attempts, personIdField);
        }

        return people
            .Select(person => new { Person = person, Id = GetString(person, "id") })
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .Select(x => new
            {
                x.Person,
                Attempts = attemptsByInduction
                    .Where(kvp => kvp.Value.TryGetValue(x.Id!, out _))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value[x.Id!], StringComparer.OrdinalIgnoreCase)
            })
            .Where(x => x.Attempts.Count > 0)
            .Select(x => MergePersonAndInductions(x.Person, x.Attempts))
            .ToList();
    }

    public async Task<IReadOnlyList<JsonElement>> GetInitialMatchRecordsAsync(SyncProfile profile, DateTimeOffset completedSince, CancellationToken ct = default)
    {
        var endpoint = profile.EntityType switch
        {
            "Staff" => "staff",
            "SpMember" => "sp/member",
            _ => null
        };
        if (endpoint == null) return Array.Empty<JsonElement>();

        var inductionIds = GetSelectedInductionIds(profile);
        if (inductionIds.Count == 0)
        {
            _logger.Information("Initial match for {ProfileId}: no inductions selected, enumerating all {Endpoint} records", profile.Id, endpoint);
            return await GetAllAsync(endpoint, ct);
        }

        _logger.Information("Initial match for {ProfileId}: scanning induction(s) {InductionIds} (mapped: {MappedIds}, selected: {SelectedIds})",
            profile.Id,
            string.Join(", ", inductionIds.OrderBy(id => id)),
            string.Join(", ", GetMappedInductionIds(profile.FieldMapJson).OrderBy(id => id)),
            string.Join(", ", GetInductionIdList(profile.SelectedInductionIdsJson).OrderBy(id => id)));

        var personIdField = GetPersonIdField(profile);
        var attemptsByInduction = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase);
        foreach (var inductionId in inductionIds)
        {
            var attempts = await _onLocation.GetInductionHoldersCompletedSinceAsync(inductionId, completedSince, ct);
            attemptsByInduction[inductionId] = LatestAttemptPerPerson(attempts, personIdField);
        }

        var eligiblePersonIds = attemptsByInduction.Values
            .SelectMany(attempts => attempts.Keys)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (eligiblePersonIds.Count == 0) return Array.Empty<JsonElement>();

        // Enumerating the whole collection costs a page request per 200 people. When only a small number of
        // people hold a relevant induction it is much cheaper to request those records individually.
        IReadOnlyList<JsonElement> people;
        if (eligiblePersonIds.Count <= TargetedFetchThreshold)
        {
            _logger.Information("Initial match for {ProfileId}: {Count} eligible {Endpoint} record(s) from {Field}, fetching each by id",
                profile.Id, eligiblePersonIds.Count, endpoint, personIdField);
            people = await _onLocation.GetRecordsByIdAsync(endpoint, eligiblePersonIds.ToList(), ct);
            // Guard against the by-id route being unavailable: enumerate rather than report nothing found.
            if (people.Count == 0)
            {
                _logger.Warning("Initial match for {ProfileId}: no records came back by id, falling back to enumerating {Endpoint}", profile.Id, endpoint);
                people = await GetAllAsync(endpoint, ct);
            }
        }
        else
        {
            _logger.Information("Initial match for {ProfileId}: {Count} eligible {Endpoint} record(s) exceeds the targeted fetch threshold of {Threshold}, enumerating instead",
                profile.Id, eligiblePersonIds.Count, endpoint, TargetedFetchThreshold);
            people = await GetAllAsync(endpoint, ct);
        }

        return people
            .Select(person => new { Person = person, Id = GetString(person, "id") })
            .Where(x => !string.IsNullOrWhiteSpace(x.Id) && eligiblePersonIds.Contains(x.Id!))
            .Select(x => new { x.Person, Attempts = attemptsByInduction.Where(kvp => kvp.Value.ContainsKey(x.Id!)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value[x.Id!], StringComparer.OrdinalIgnoreCase) })
            .Where(x => x.Attempts.Count > 0)
            .Select(x => MergePersonAndInductions(x.Person, x.Attempts))
            .ToList();
    }

    private static string GetPersonIdField(SyncProfile profile) => profile.EntityType == "SpMember" ? "sp_member_id" : "staff_id";

    private static Dictionary<string, JsonElement> LatestAttemptPerPerson(IReadOnlyList<JsonElement> attempts, string personIdField) => attempts
        .Select(attempt => new { Attempt = attempt, PersonId = GetString(attempt, personIdField) })
        .Where(x => !string.IsNullOrWhiteSpace(x.PersonId) && x.PersonId != "0")
        .GroupBy(x => x.PersonId!, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.OrderByDescending(x => GetCompletedAt(x.Attempt)).First().Attempt, StringComparer.OrdinalIgnoreCase);

    private static DateTimeOffset GetCompletedAt(JsonElement attempt)
    {
        foreach (var name in new[] { "completed", "modified", "created" })
        {
            var value = GetString(attempt, name);
            if (!string.IsNullOrWhiteSpace(value) && DateTimeOffset.TryParse(value, out var parsed)) return parsed;
        }
        return DateTimeOffset.MinValue;
    }

    private const int InitialMatchPageSize = 200;
    private const int InitialMatchMaxPages = 200;
    private const int TargetedFetchThreshold = 150;

    private async Task<IReadOnlyList<JsonElement>> GetAllAsync(string endpoint, CancellationToken ct)
    {
        var all = new List<JsonElement>();
        var cursor = new SyncBookmark { ProfileId = "initial-match" };
        for (var page = 0; page < InitialMatchMaxPages && !ct.IsCancellationRequested; page++)
        {
            var previousCursor = cursor.Cursor;
            var batch = endpoint == "staff"
                ? await _onLocation.GetStaffAsync(cursor, ct, InitialMatchPageSize)
                : await _onLocation.GetContractorMembersAsync(cursor, ct, InitialMatchPageSize);
            all.AddRange(batch);
            if (batch.Count < InitialMatchPageSize || string.IsNullOrWhiteSpace(cursor.Cursor) || cursor.Cursor == previousCursor) break;
        }
        return all;
    }

    // Inductions referenced by the field map are the candidates; the profile's selection narrows them so an
    // operator can exclude an induction that is mapped but not relevant to this interface.
    public static HashSet<string> GetSelectedInductionIds(SyncProfile profile)
    {
        var mapped = GetMappedInductionIds(profile.FieldMapJson);
        var selected = GetInductionIdList(profile.SelectedInductionIdsJson);
        if (selected.Count == 0) return mapped;

        mapped.IntersectWith(selected);
        return mapped;
    }

    public static HashSet<string> GetInductionIdList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var ids = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            return ids.Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static HashSet<string> GetMappedInductionIds(string fieldMapJson)
    {
        var maps = JsonSerializer.Deserialize<List<FieldMapDto>>(fieldMapJson) ?? new List<FieldMapDto>();
        return maps
            .Select(m => m.Source.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length >= 3 && string.Equals(parts[0], "inductions", StringComparison.OrdinalIgnoreCase))
            .Select(parts => parts[1])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static JsonElement MergePersonAndInductions(JsonElement person, Dictionary<string, JsonElement> attempts)
    {
        var merged = JsonNode.Parse(person.GetRawText())!.AsObject();
        var inductions = new JsonObject();
        foreach (var attempt in attempts)
            inductions[attempt.Key] = JsonNode.Parse(attempt.Value.GetRawText());
        merged["inductions"] = inductions;
        return JsonSerializer.SerializeToElement(merged);
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ValueKind == JsonValueKind.Number ? value.GetRawText() : null;
    }
}
