using System.Text.Json;
using System.Text.Json.Serialization;

namespace OnLocationGallagherBridge.Models;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(BridgeConfig))]
[JsonSerializable(typeof(List<FieldMapDto>))]
[JsonSerializable(typeof(List<MatchRuleDto>))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}

public class FieldMapDto
{
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Transform { get; set; } = "copy";
    public Dictionary<string, object>? Options { get; set; }
}

public class MatchRuleDto
{
    public string SourceFields { get; set; } = string.Empty;
    public string TargetFields { get; set; } = string.Empty;
    public string MatchType { get; set; } = "exact"; // exact, fuzzy
    public int Priority { get; set; }
    public bool IsPrimary { get; set; }

    // UI-only projections of the comma separated field strings, so the mapping page can use multi-selects.
    [JsonIgnore]
    public List<string> SourceFieldList { get; set; } = new();

    [JsonIgnore]
    public List<string> TargetFieldList { get; set; } = new();
}
