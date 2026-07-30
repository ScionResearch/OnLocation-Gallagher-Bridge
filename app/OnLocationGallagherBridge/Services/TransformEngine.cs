using System.Globalization;
using System.Text.Json;
using OnLocationGallagherBridge.Models;

namespace OnLocationGallagherBridge.Services;

public interface ITransformEngine
{
    Dictionary<string, object?> Transform(SyncProfile profile, JsonElement source);
    Dictionary<string, object?> ApplyCreateDefaults(SyncProfile profile, Dictionary<string, object?> payload);
    Task<Dictionary<string, object?>> BuildCardholderPayloadAsync(Dictionary<string, object?> transformed, string? cardholderHref, CancellationToken ct = default);
}

public class GallagherReference
{
    public string Href { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class TransformEngine : ITransformEngine
{
    private readonly IGallagherConnector _gallagher;
    private readonly Serilog.ILogger _logger;

    public TransformEngine(IGallagherConnector gallagher, Serilog.ILogger? logger = null)
    {
        _gallagher = gallagher;
        _logger = logger ?? Serilog.Log.Logger.ForContext<TransformEngine>();
    }

    public Dictionary<string, object?> Transform(SyncProfile profile, JsonElement source)
    {
        var output = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(profile.FieldMapJson)) return output;

        var mappings = JsonSerializer.Deserialize<List<FieldMapDto>>(profile.FieldMapJson) ?? new List<FieldMapDto>();

        foreach (var map in mappings)
        {
            var value = map.Transform?.ToLowerInvariant() switch
            {
                "static" => map.Options?.TryGetValue("value", out var staticVal) == true ? staticVal : null,
                "date" => TryFormatDate(GetSourceValue(source, map.Source), map.Options),
                "lookup" => Lookup(GetSourceValue(source, map.Source), map.Options),
                "condition" => ApplyCondition(GetSourceValue(source, map.Source), map.Options),
                "concat" => Concat(source, map.Options),
                "first-name" => GetFirstName(GetSourceValue(source, map.Source)),
                "last-name" => GetLastName(GetSourceValue(source, map.Source)),
                "gallagher-expiry" => FormatGallagherExpiry(GetSourceValue(source, map.Source)),
                "rule-based" => EvaluateRuleBasedTransform(source, map),
                _ => GetSourceValue(source, map.Source)
            };

            if (value != null) output[ToGallagherFieldName(map.Target)] = value;
        }

        return output;
    }

    // Command Centre rejects a cardholder without a division, and access group membership is what actually
    // grants access, so both come from the profile defaults. Applied on create only: sending a division on
    // every update would drag cardholders back into the default division after an operator moved them.
    public Dictionary<string, object?> ApplyCreateDefaults(SyncProfile profile, Dictionary<string, object?> payload)
    {
        if (!payload.ContainsKey("division") && !string.IsNullOrWhiteSpace(profile.DefaultDivisionHref))
            payload["division"] = new Dictionary<string, object?> { ["href"] = profile.DefaultDivisionHref };

        var accessGroups = ParseReferences(profile.DefaultAccessGroupsJson)
            .Where(group => !string.IsNullOrWhiteSpace(group.Href))
            .Select(group => new Dictionary<string, object?>
            {
                ["accessGroup"] = new Dictionary<string, object?> { ["href"] = group.Href }
            })
            .ToList();
        if (accessGroups.Count > 0 && !payload.ContainsKey("accessGroups"))
            payload["accessGroups"] = accessGroups;

        return payload;
    }

    // Competency values arrive as flat 'competencies.<name>.<field>' keys from the mapping UI. Command Centre
    // wants them nested under a competency link, and the shape differs by verb: POST takes a plain array,
    // PATCH takes an add/update envelope. Sending the flat keys is silently ignored by the server.
    public async Task<Dictionary<string, object?>> BuildCardholderPayloadAsync(Dictionary<string, object?> transformed, string? cardholderHref, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var competencyValues = new Dictionary<string, Dictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in transformed)
        {
            if (!field.Key.StartsWith("competencies.", StringComparison.OrdinalIgnoreCase))
            {
                payload[field.Key] = field.Value;
                continue;
            }

            var parts = field.Key.Split('.', 3);
            if (parts.Length != 3 || string.IsNullOrWhiteSpace(parts[1])) continue;
            if (!competencyValues.TryGetValue(parts[1], out var values))
            {
                values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                competencyValues[parts[1]] = values;
            }

            values[parts[2].Equals("expires", StringComparison.OrdinalIgnoreCase) ? "expiry" : parts[2]] = field.Value;
        }

        if (competencyValues.Count == 0) return payload;

        var isCreate = string.IsNullOrWhiteSpace(cardholderHref);
        var existingCompetencies = isCreate
            ? null
            : await _gallagher.GetCardholderAsync(cardholderHref!, "competencies", ct);
        var definitions = await _gallagher.GetCompetenciesAsync(ct);
        var add = new List<Dictionary<string, object?>>();
        var update = new List<Dictionary<string, object?>>();

        foreach (var entry in competencyValues)
        {
            var current = existingCompetencies.HasValue ? FindCompetency(existingCompetencies.Value, entry.Key) : null;
            if (current.HasValue && GetString(current.Value, "href") is { } linkHref)
            {
                var values = new Dictionary<string, object?>(entry.Value, StringComparer.OrdinalIgnoreCase)
                {
                    ["href"] = linkHref
                };
                update.Add(values);
                continue;
            }

            var definition = definitions.FirstOrDefault(c => string.Equals(GetCompetencyName(c), entry.Key, StringComparison.OrdinalIgnoreCase));
            var definitionHref = definition.ValueKind == JsonValueKind.Undefined ? null : GetHref(definition);
            if (string.IsNullOrWhiteSpace(definitionHref))
            {
                _logger.Warning("Competency '{Competency}' is mapped but no competency of that name exists in Gallagher, so it cannot be applied", entry.Key);
                continue;
            }

            var addValues = new Dictionary<string, object?>(entry.Value, StringComparer.OrdinalIgnoreCase)
            {
                ["competency"] = new Dictionary<string, string> { ["href"] = definitionHref }
            };
            add.Add(addValues);
        }

        if (isCreate)
        {
            if (add.Count > 0) payload["competencies"] = add;
            _logger.Information("Cardholder create payload includes {Count} competency assignment(s)", add.Count);
            return payload;
        }

        if (add.Count > 0 || update.Count > 0)
        {
            var competencies = new Dictionary<string, object?>();
            if (add.Count > 0) competencies["add"] = add;
            if (update.Count > 0) competencies["update"] = update;
            payload["competencies"] = competencies;
            _logger.Information("Cardholder update payload adds {Added} and updates {Updated} competency assignment(s)", add.Count, update.Count);
        }

        return payload;
    }

    private static JsonElement? FindCompetency(JsonElement cardholder, string name)
    {
        if (!cardholder.TryGetProperty("competencies", out var competencies) || competencies.ValueKind != JsonValueKind.Array) return null;
        foreach (var competency in competencies.EnumerateArray())
        {
            if (string.Equals(GetCompetencyName(competency), name, StringComparison.OrdinalIgnoreCase)) return competency;
        }
        return null;
    }

    private static string? GetCompetencyName(JsonElement competency)
    {
        if (competency.TryGetProperty("competency", out var definition) && definition.ValueKind == JsonValueKind.Object)
            return GetString(definition, "name");
        return GetString(competency, "name");
    }

    private static string? GetHref(JsonElement element)
    {
        if (element.TryGetProperty("href", out var href) && href.ValueKind == JsonValueKind.String) return href.GetString();
        return null;
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
        }
        return null;
    }

    public static List<GallagherReference> ParseReferences(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<GallagherReference>();
        try
        {
            return JsonSerializer.Deserialize<List<GallagherReference>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new List<GallagherReference>();
        }
        catch (JsonException)
        {
            return new List<GallagherReference>();
        }
    }

    // The mapping UI addresses Gallagher personal data fields as 'personalDataFields.<name>' because that
    // is the name of the 'fields' request specifier, but Command Centre reads and writes the values at the
    // cardholder root with the PDF name prefixed by '@'.
    public static string ToGallagherFieldName(string target)
    {
        var segments = target.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 2 || !string.Equals(segments[0], "personalDataFields", StringComparison.OrdinalIgnoreCase))
            return target;

        return segments[1].StartsWith('@') ? segments[1] : $"@{segments[1]}";
    }

    private static object? GetSourceValue(JsonElement source, string sourceField)
    {
        var value = source;
        foreach (var segment in sourceField.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value)) return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString() as object,
            JsonValueKind.Number => value.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => value.GetRawText()
        };
    }

    private static string? FormatGallagherExpiry(object? input)
    {
        var text = input?.ToString();
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var date))
        {
            var localEnd = DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(23, 59, 59)), DateTimeKind.Unspecified);
            var zone = GetNewZealandTimeZone();
            return TimeZoneInfo.ConvertTimeToUtc(localEnd, zone).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var timestamp)
            ? timestamp.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
            : null;
    }

    private static TimeZoneInfo GetNewZealandTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time"); }
    }

    private static string? GetFirstName(object? input)
    {
        var parts = SplitName(input);
        return parts.Length == 0 ? null : parts[0];
    }

    private static string? GetLastName(object? input)
    {
        var parts = SplitName(input);
        return parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : null;
    }

    private static string[] SplitName(object? input)
    {
        return input?.ToString()?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? Array.Empty<string>();
    }

    private static object? TryFormatDate(object? input, Dictionary<string, object>? options)
    {
        if (input == null || options == null) return input;
        if (DateTimeOffset.TryParse(input.ToString(), out var dto))
        {
            var format = options.TryGetValue("format", out var fmt) ? fmt?.ToString() : "yyyy-MM-dd";
            return dto.ToString(format);
        }
        return input;
    }

    private static object? Lookup(object? input, Dictionary<string, object>? options)
    {
        if (input == null || options == null) return input;
        var tableName = options.TryGetValue("lookupTable", out var tn) ? tn?.ToString() : null;
        if (string.IsNullOrEmpty(tableName) || !options.TryGetValue("values", out var vals)) return input;
        if (vals is JsonElement je && je.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in je.EnumerateObject())
                if (string.Equals(prop.Name, input.ToString(), StringComparison.OrdinalIgnoreCase))
                    return prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.GetRawText();
        }
        return input;
    }

    private static object? ApplyCondition(object? input, Dictionary<string, object>? options)
    {
        if (input == null || options == null) return input;
        if (options.TryGetValue("rules", out var rulesVal) && rulesVal is JsonElement rules && rules.ValueKind == JsonValueKind.Array)
        {
            foreach (var rule in rules.EnumerateArray())
            {
                if (rule.TryGetProperty("eq", out var eq) && ValueEquals(input, eq)) return GetOutput(rule);
                if (rule.TryGetProperty("else", out var _)) return GetOutput(rule);
            }
        }
        return input;
    }

    private static object? GetOutput(JsonElement rule)
    {
        if (rule.TryGetProperty("out", out var o))
            return o.ValueKind == JsonValueKind.String ? o.GetString() : o.GetRawText();
        return null;
    }

    private static bool ValueEquals(object? input, JsonElement expected)
    {
        var expectedStr = expected.ValueKind == JsonValueKind.String ? expected.GetString() : expected.GetRawText();
        return string.Equals(input?.ToString(), expectedStr, StringComparison.OrdinalIgnoreCase);
    }

    private static object? Concat(JsonElement source, Dictionary<string, object>? options)
    {
        if (options == null) return null;
        if (options.TryGetValue("fields", out var fieldsVal) && fieldsVal is JsonElement fields && fields.ValueKind == JsonValueKind.Array)
        {
            var sep = options.TryGetValue("separator", out var s) ? s?.ToString() ?? " " : " ";
            var parts = new List<string>();
            foreach (var f in fields.EnumerateArray())
            {
                var name = f.GetString();
                var v = GetSourceValue(source, name ?? "");
                if (v != null) parts.Add(v.ToString() ?? "");
            }
            return string.Join(sep, parts);
        }
        return null;
    }

    private static object? EvaluateRuleBasedTransform(JsonElement source, FieldMapDto map)
    {
        if (map.Rules == null || map.Rules.Count == 0) return null;
        var isAnd = !string.Equals(map.RuleLogic, "or", StringComparison.OrdinalIgnoreCase);
        var matched = isAnd;

        foreach (var rule in map.Rules)
        {
            var condition = EvaluateRule(source, rule);
            matched = isAnd ? matched && condition : matched || condition;
            if (isAnd && !matched) break;
            if (!isAnd && matched) break;
        }

        if (!matched) return null;

        if (!string.IsNullOrWhiteSpace(map.RuleOutputSource))
            return GetSourceValue(source, map.RuleOutputSource);

        if (string.IsNullOrWhiteSpace(map.RuleOutputValue)) return null;
        if (map.RuleOutputIsNumber && decimal.TryParse(map.RuleOutputValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
            return number;
        return map.RuleOutputValue;
    }

    private static bool EvaluateRule(JsonElement source, FieldMapRuleDto rule)
    {
        var left = GetSourceValue(source, rule.Source);
        var right = rule.ValueIsSource ? GetSourceValue(source, rule.Value ?? string.Empty) : rule.Value;
        var op = rule.Operator?.ToLowerInvariant() ?? "equals";

        switch (op)
        {
            case "exists":
                return left != null;
            case "equals":
                return string.Equals(left?.ToString(), right?.ToString(), StringComparison.OrdinalIgnoreCase);
            case "not-equals":
                return !string.Equals(left?.ToString(), right?.ToString(), StringComparison.OrdinalIgnoreCase);
            case "contains":
                return left?.ToString()?.Contains(right?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true;
            case "not-contains":
                return left?.ToString()?.Contains(right?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase) != true;
            case "greater-than":
                return CompareNumeric(left, right) > 0;
            case "less-than":
                return CompareNumeric(left, right) < 0;
            default:
                return false;
        }
    }

    private static int CompareNumeric(object? left, object? right)
    {
        if (!decimal.TryParse(left?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var l))
            return int.MinValue;
        if (!decimal.TryParse(right?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var r))
            return int.MinValue;
        return l.CompareTo(r);
    }
}
