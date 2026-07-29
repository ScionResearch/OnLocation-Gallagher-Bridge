using System.Linq;
using System.Text.Json;
using OnLocationGallagherBridge.Models;

namespace OnLocationGallagherBridge.Services;

public interface IIdentityMatcher
{
    Task<MatchResult?> FindBestMatchAsync(SyncProfile profile, JsonElement sourceRecord, IReadOnlyList<JsonElement> candidates, CancellationToken ct = default);

    IMatchSession CreateSession(SyncProfile profile, IReadOnlyList<JsonElement> candidates);
}

public interface IMatchSession
{
    MatchResult? Match(JsonElement sourceRecord);
}

public class MatchResult
{
    public string? GallagherHref { get; set; }
    public double Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool RequiresManualReview { get; set; }
}

public class IdentityMatcher : IIdentityMatcher
{
    public const int FuzzyMaxDistance = 2;
    public const double FuzzyConfidence = 0.8;

    private readonly Serilog.ILogger _logger;

    public IdentityMatcher(Serilog.ILogger? logger = null)
    {
        _logger = logger ?? Serilog.Log.Logger.ForContext<IdentityMatcher>();
    }

    public Task<MatchResult?> FindBestMatchAsync(SyncProfile profile, JsonElement sourceRecord, IReadOnlyList<JsonElement> candidates, CancellationToken ct = default)
        => Task.FromResult(CreateSession(profile, candidates).Match(sourceRecord));

    public IMatchSession CreateSession(SyncProfile profile, IReadOnlyList<JsonElement> candidates)
        => new MatchSession(profile, candidates, _logger);

    private sealed class MatchSession : IMatchSession
    {
        private readonly List<CompiledRule> _rules = new();
        private readonly HashSet<string> _assignedHrefs = new(StringComparer.OrdinalIgnoreCase);

        public MatchSession(SyncProfile profile, IReadOnlyList<JsonElement> candidates, Serilog.ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(profile.MatchRulesJson)) return;

            var rules = JsonSerializer.Deserialize<List<MatchRuleDto>>(profile.MatchRulesJson) ?? new List<MatchRuleDto>();
            foreach (var rule in rules.OrderByDescending(r => r.IsPrimary).ThenBy(r => r.Priority).ThenBy(r => r.SourceFields))
            {
                var compiled = CompiledRule.Create(rule, candidates);
                if (compiled == null)
                {
                    logger.Warning("Match rule '{Source}' => '{Target}' was skipped: source and target field counts must match and be non-empty", rule.SourceFields, rule.TargetFields);
                    continue;
                }

                _rules.Add(compiled);
                if (compiled.CandidatesWithValues == 0)
                    logger.Warning("Match rule {Rule} cannot match: none of the {CandidateCount} Gallagher cardholders have a value for '{Target}'. Check the field name and that it is included in the cardholder fields request", compiled.Description, candidates.Count, rule.TargetFields);
                else
                    logger.Information("Match rule {Rule} ready: {WithValues} of {CandidateCount} Gallagher cardholders have a usable value", compiled.Description, compiled.CandidatesWithValues, candidates.Count);
            }

            if (_rules.Count == 0) logger.Warning("No usable match rules were compiled for profile {ProfileId}", profile.Id);
        }

        public MatchResult? Match(JsonElement sourceRecord)
        {
            MatchResult? needsReview = null;
            foreach (var rule in _rules)
            {
                var result = rule.Match(sourceRecord, _assignedHrefs);
                if (result == null) continue;
                if (string.IsNullOrWhiteSpace(result.GallagherHref))
                {
                    needsReview ??= result;
                    continue;
                }

                if (!result.RequiresManualReview) _assignedHrefs.Add(result.GallagherHref);
                return result;
            }

            return needsReview;
        }
    }

    private sealed class CompiledRule
    {
        private readonly string _description;
        private readonly bool _fuzzy;
        private readonly List<string> _sourceFields;
        private readonly bool _collapseSource;
        private readonly Dictionary<string, List<string>> _exactIndex;
        private readonly List<(string Href, string[] Values)> _candidateValues;

        public string Description => _description;
        public int CandidatesWithValues { get; private init; }

        private CompiledRule(string description, bool fuzzy, List<string> sourceFields, bool collapseSource, Dictionary<string, List<string>> exactIndex, List<(string, string[])> candidateValues)
        {
            _description = description;
            _fuzzy = fuzzy;
            _sourceFields = sourceFields;
            _collapseSource = collapseSource;
            _exactIndex = exactIndex;
            _candidateValues = candidateValues;
        }

        public static CompiledRule? Create(MatchRuleDto rule, IReadOnlyList<JsonElement> candidates)
        {
            var sourceFields = SplitFields(rule.SourceFields);
            var targetFields = SplitFields(rule.TargetFields);
            if (sourceFields.Count == 0 || targetFields.Count == 0) return null;

            // Field counts normally line up one-to-one. When one side is a single field and the other is
            // several, the several are joined with a space and compared as one value. This is what lets
            // OnLocation's combined 'name' match Gallagher's separate firstName and lastName.
            var collapseSource = sourceFields.Count > 1 && targetFields.Count == 1;
            var collapseTarget = targetFields.Count > 1 && sourceFields.Count == 1;
            if (sourceFields.Count != targetFields.Count && !collapseSource && !collapseTarget) return null;

            var fuzzy = string.Equals(rule.MatchType, "fuzzy", StringComparison.OrdinalIgnoreCase);
            var exactIndex = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var candidateValues = new List<(string, string[])>(candidates.Count);
            var withValues = 0;

            foreach (var candidate in candidates)
            {
                var href = GetString(candidate, "href");
                if (string.IsNullOrWhiteSpace(href)) continue;

                var values = ReadValues(candidate, targetFields, collapseTarget);
                if (values == null) continue;

                withValues++;
                var key = BuildKey(values);
                if (!exactIndex.TryGetValue(key, out var hrefs)) exactIndex[key] = hrefs = new List<string>();
                if (!hrefs.Contains(href, StringComparer.OrdinalIgnoreCase)) hrefs.Add(href);
                if (fuzzy) candidateValues.Add((href, values));
            }

            var description = $"'{rule.SourceFields}' => '{rule.TargetFields}' ({(fuzzy ? "fuzzy" : "exact")})";
            return new CompiledRule(description, fuzzy, sourceFields, collapseSource, exactIndex, candidateValues) { CandidatesWithValues = withValues };
        }

        public MatchResult? Match(JsonElement sourceRecord, HashSet<string> assignedHrefs)
        {
            var sourceValues = ReadValues(sourceRecord, _sourceFields, _collapseSource);
            if (sourceValues == null) return null;

            if (_exactIndex.TryGetValue(BuildKey(sourceValues), out var exactHrefs) && exactHrefs.Count > 0)
                return Resolve(exactHrefs, assignedHrefs, 1.0, _description);

            if (!_fuzzy) return null;

            var bestDistance = int.MaxValue;
            var bestHrefs = new List<string>();
            foreach (var (href, values) in _candidateValues)
            {
                if (!TryFuzzyDistance(sourceValues, values, out var distance) || distance > bestDistance) continue;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestHrefs.Clear();
                }
                bestHrefs.Add(href);
            }

            if (bestHrefs.Count == 0) return null;
            return Resolve(bestHrefs, assignedHrefs, FuzzyConfidence, $"{_description} distance {bestDistance}", alwaysReview: true);
        }

        private MatchResult Resolve(List<string> hrefs, HashSet<string> assignedHrefs, double confidence, string reason, bool alwaysReview = false)
        {
            if (hrefs.Count > 1)
                return new MatchResult { Confidence = 0, RequiresManualReview = true, Reason = $"{reason}: {hrefs.Count} Gallagher cardholders matched, resolve manually" };

            var href = hrefs[0];
            if (assignedHrefs.Contains(href))
                return new MatchResult { Confidence = 0, RequiresManualReview = true, Reason = $"{reason}: cardholder already matched to another OnLocation record" };

            return new MatchResult { GallagherHref = href, Confidence = confidence, Reason = reason, RequiresManualReview = alwaysReview };
        }

        private static bool TryFuzzyDistance(string[] source, string[] target, out int distance)
        {
            distance = 0;
            for (var i = 0; i < source.Length; i++)
            {
                if (!TryLevenshtein(source[i], target[i], FuzzyMaxDistance, out var componentDistance)) return false;
                distance += componentDistance;
                if (distance > FuzzyMaxDistance) return false;
            }
            return true;
        }

        private static List<string> SplitFields(string fields) => fields
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        private static string BuildKey(string[] values) => string.Join('\u0001', values);

        private static string[]? ReadValues(JsonElement element, List<string> fields, bool collapse)
        {
            var values = new string[fields.Count];
            for (var i = 0; i < fields.Count; i++)
            {
                var value = GetString(element, fields[i]);
                if (string.IsNullOrWhiteSpace(value)) return null;
                values[i] = Normalize(value);
            }

            return collapse ? new[] { string.Join(' ', values) } : values;
        }
    }

    private static bool TryLevenshtein(string a, string b, int maxDistance, out int distance)
    {
        distance = 0;
        if (string.Equals(a, b, StringComparison.Ordinal)) return true;
        if (Math.Abs(a.Length - b.Length) > maxDistance) return false;

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var rowMinimum = current[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                rowMinimum = Math.Min(rowMinimum, current[j]);
            }

            if (rowMinimum > maxDistance) return false;
            (previous, current) = (current, previous);
        }

        distance = previous[b.Length];
        return distance <= maxDistance;
    }

    private static string? TryGetPersonalDataField(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;

        var bare = name.StartsWith('@') ? name[1..] : name;
        var prefixed = $"@{bare}";

        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, prefixed, StringComparison.OrdinalIgnoreCase)) continue;
            return property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }

        return null;
    }

    private static string Normalize(string value)
    {
        var parts = value.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" ", parts).ToLowerInvariant();
    }

    internal static string? GetString(JsonElement element, string path)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Gallagher personal data fields are addressed as 'personalDataFields.<name>' in the mapping UI
        // because that is the name of the 'fields' request specifier, but the response carries the values
        // at the cardholder root with the PDF name prefixed by '@'. Translate before traversing.
        if (segments.Length == 2 && string.Equals(segments[0], "personalDataFields", StringComparison.OrdinalIgnoreCase))
        {
            var pdf = TryGetPersonalDataField(element, segments[1]);
            if (pdf is not null) return pdf;
        }

        var current = element;
        foreach (var segment in segments)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next)) return null;
            current = next;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }
}
