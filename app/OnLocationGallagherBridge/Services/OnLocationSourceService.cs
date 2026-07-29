using System.Text.Json;
using System.Text.Json.Nodes;
using OnLocationGallagherBridge.Models;

namespace OnLocationGallagherBridge.Services;

public interface IOnLocationSourceService
{
    Task<IReadOnlyList<JsonElement>> GetRecordsAsync(SyncProfile profile, SyncBookmark bookmark, CancellationToken ct = default);
    Task<IReadOnlyList<JsonElement>> GetInitialMatchRecordsAsync(SyncProfile profile, DateTimeOffset completedSince, IProgress<OnLocationFetchProgress>? progress = null, CancellationToken ct = default);
}

public record OnLocationFetchProgress(int Percent, string Message);

public class OnLocationSourceService : IOnLocationSourceService
{
    private readonly IOnLocationConnector _onLocation;
    private readonly ISyncActivity _activity;
    private readonly Serilog.ILogger _logger;

    public OnLocationSourceService(IOnLocationConnector onLocation, ISyncActivity activity, Serilog.ILogger? logger = null)
    {
        _onLocation = onLocation;
        _activity = activity;
        _logger = logger ?? Serilog.Log.Logger.ForContext<OnLocationSourceService>();
    }

    public async Task<IReadOnlyList<JsonElement>> GetRecordsAsync(SyncProfile profile, SyncBookmark bookmark, CancellationToken ct = default)
    {
        var inductionIds = GetSelectedInductionIds(profile);
        if (inductionIds.Count == 0)
        {
            // No induction drives this profile, so the people list itself is walked a page at a time.
            return profile.EntityType switch
            {
                "Staff" => await _onLocation.GetStaffAsync(bookmark, ct),
                "SpMember" => await _onLocation.GetContractorMembersAsync(bookmark, ct),
                _ => Array.Empty<JsonElement>()
            };
        }

        // Only holder records created since the last poll matter, so the highest id seen per induction is kept
        // and used as the filter. This replaces walking the whole member list on every run, which was both slow
        // and the reason unrelated people kept arriving in the manual match queue.
        var personIdField = GetPersonIdField(profile);
        var cursors = ParseCursors(bookmark.InductionCursorsJson);
        var attemptsByInduction = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase);
        var completedSince = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, profile.SyncWindowDays));

        _logger.Information("Profile {Profile}: scanning {Count} induction(s) for records completed on or after {Since:yyyy-MM-dd} ({Days} day window)",
            profile.Id, inductionIds.Count, completedSince, profile.SyncWindowDays);

        var index = 0;
        foreach (var inductionId in inductionIds)
        {
            index++;
            cursors.TryGetValue(inductionId, out var afterId);
            _activity.SetPhase($"Scanning induction {index} of {inductionIds.Count}", $"Induction {inductionId} from id {afterId ?? "(window start)"}");

            var scan = await _onLocation.GetNewInductionHoldersAsync(inductionId, afterId, completedSince, ct);

            // The bookmark advances past everything seen, including records the window excluded, otherwise
            // they would be re-read on every poll for ever.
            if (!string.IsNullOrWhiteSpace(scan.HighestId)) cursors[inductionId] = scan.HighestId!;
            if (scan.Records.Count == 0) continue;

            attemptsByInduction[inductionId] = LatestAttemptPerPerson(scan.Records, personIdField);
        }

        bookmark.InductionCursorsJson = JsonSerializer.Serialize(cursors);

        var personIds = attemptsByInduction.Values
            .SelectMany(attempts => attempts.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (personIds.Count == 0)
        {
            _logger.Information("Profile {Profile}: no new induction records since the last poll", profile.Id);
            return Array.Empty<JsonElement>();
        }

        var endpoint = profile.EntityType switch
        {
            "Staff" => "staff",
            "SpMember" => "sp/member",
            _ => null
        };
        if (endpoint == null) return Array.Empty<JsonElement>();

        _activity.SetPhase("Fetching people", $"{personIds.Count} person record(s) affected by new induction records");
        _activity.AddMatched(personIds.Count);
        var people = await _onLocation.GetRecordsByIdAsync(endpoint, personIds, ct);
        _logger.Information("Profile {Profile}: {Attempts} new induction record(s) affecting {People} person record(s)",
            profile.Id, attemptsByInduction.Values.Sum(a => a.Count), people.Count);

        return people
            .Select(person => new { Person = person, Id = GetString(person, "id") })
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .Select(x => new
            {
                x.Person,
                Attempts = attemptsByInduction
                    .Where(kvp => kvp.Value.ContainsKey(x.Id!))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value[x.Id!], StringComparer.OrdinalIgnoreCase)
            })
            .Where(x => x.Attempts.Count > 0)
            .Select(x => MergePersonAndInductions(x.Person, x.Attempts))
            .ToList();
    }

    private static Dictionary<string, string> ParseCursors(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) is { } parsed
                ? new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task<IReadOnlyList<JsonElement>> GetInitialMatchRecordsAsync(SyncProfile profile, DateTimeOffset completedSince, IProgress<OnLocationFetchProgress>? progress = null, CancellationToken ct = default)
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
            ReportProgress(progress, 0, "No inductions selected; enumerating all records...");
            return await GetAllAsync(endpoint, ct, progress, 0, 100);
        }

        _logger.Information("Initial match for {ProfileId}: scanning induction(s) {InductionIds} (mapped: {MappedIds}, selected: {SelectedIds})",
            profile.Id,
            string.Join(", ", inductionIds.OrderBy(id => id)),
            string.Join(", ", GetMappedInductionIds(profile.FieldMapJson).OrderBy(id => id)),
            string.Join(", ", GetInductionIdList(profile.SelectedInductionIdsJson).OrderBy(id => id)));

        ReportProgress(progress, 0, $"Scanning {inductionIds.Count} induction(s) for completed records...");

        var personIdField = GetPersonIdField(profile);
        var attemptsByInduction = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        var perInductionRange = 40.0 / inductionIds.Count;
        foreach (var inductionId in inductionIds)
        {
            var inductionIndex = index;
            var inductionProgress = new Progress<OnLocationFetchProgress>(p =>
            {
                var overallPercent = (int)(inductionIndex * perInductionRange + p.Percent / 100.0 * perInductionRange);
                ReportProgress(progress, Math.Min(40, overallPercent), p.Message);
            });
            var attempts = await _onLocation.GetInductionHoldersCompletedSinceAsync(inductionId, completedSince, inductionProgress, ct);
            attemptsByInduction[inductionId] = LatestAttemptPerPerson(attempts, personIdField);
            index++;
            var totalAttempts = attemptsByInduction.Values.Sum(a => a.Count);
            var pct = (int)(index * 40.0 / inductionIds.Count);
            ReportProgress(progress, Math.Min(40, pct), $"Scanned {index}/{inductionIds.Count} induction(s); {totalAttempts} eligible record(s) found.");
        }

        var eligiblePersonIds = attemptsByInduction.Values
            .SelectMany(attempts => attempts.Keys)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (eligiblePersonIds.Count == 0)
        {
            ReportProgress(progress, 100, "No eligible records found.");
            return Array.Empty<JsonElement>();
        }

        IReadOnlyList<JsonElement> people;
        if (eligiblePersonIds.Count <= TargetedFetchThreshold)
        {
            _logger.Information("Initial match for {ProfileId}: {Count} eligible {Endpoint} record(s) from {Field}, fetching each by id",
                profile.Id, eligiblePersonIds.Count, endpoint, personIdField);
            ReportProgress(progress, 40, $"Fetching {eligiblePersonIds.Count} person record(s)...");
            people = await _onLocation.GetRecordsByIdAsync(endpoint, eligiblePersonIds.ToList(), ct);
            ReportProgress(progress, 80, $"Fetched {people.Count} person record(s).");
            if (people.Count == 0)
            {
                _logger.Warning("Initial match for {ProfileId}: no records came back by id, falling back to enumerating {Endpoint}", profile.Id, endpoint);
                ReportProgress(progress, 80, "No records returned by id; falling back to enumeration...");
                people = await GetAllAsync(endpoint, ct, progress, 80, 20);
            }
        }
        else
        {
            _logger.Information("Initial match for {ProfileId}: {Count} eligible {Endpoint} record(s) exceeds the targeted fetch threshold of {Threshold}, enumerating instead",
                profile.Id, eligiblePersonIds.Count, endpoint, TargetedFetchThreshold);
            ReportProgress(progress, 40, $"Enumerating {endpoint} records...");
            people = await GetAllAsync(endpoint, ct, progress, 40, 60);
        }

        ReportProgress(progress, 100, $"Loaded {people.Count} records; merging induction history...");
        return people
            .Select(person => new { Person = person, Id = GetString(person, "id") })
            .Where(x => !string.IsNullOrWhiteSpace(x.Id) && eligiblePersonIds.Contains(x.Id!))
            .Select(x => new { x.Person, Attempts = attemptsByInduction.Where(kvp => kvp.Value.ContainsKey(x.Id!)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value[x.Id!], StringComparer.OrdinalIgnoreCase) })
            .Where(x => x.Attempts.Count > 0)
            .Select(x => MergePersonAndInductions(x.Person, x.Attempts))
            .ToList();
    }

    private static void ReportProgress(IProgress<OnLocationFetchProgress>? progress, int percent, string message)
    {
        progress?.Report(new OnLocationFetchProgress(percent, message));
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

    private async Task<IReadOnlyList<JsonElement>> GetAllAsync(string endpoint, CancellationToken ct, IProgress<OnLocationFetchProgress>? progress = null, int progressBase = 0, int progressRange = 100)
    {
        const int assumedPages = 10;
        var all = new List<JsonElement>();
        var cursor = new SyncBookmark { ProfileId = "initial-match" };
        for (var page = 0; page < InitialMatchMaxPages && !ct.IsCancellationRequested; page++)
        {
            var previousCursor = cursor.Cursor;
            var batch = endpoint == "staff"
                ? await _onLocation.GetStaffAsync(cursor, ct, InitialMatchPageSize)
                : await _onLocation.GetContractorMembersAsync(cursor, ct, InitialMatchPageSize);
            all.AddRange(batch);
            var reportedPage = Math.Min(page + 1, assumedPages);
            var percent = progressBase + (int)(reportedPage * (double)progressRange / assumedPages);
            ReportProgress(progress, Math.Min(progressBase + progressRange, percent), $"Retrieved {all.Count} records...");
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
