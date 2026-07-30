using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OnLocationGallagherBridge.Data;
using OnLocationGallagherBridge.Models;
using OnLocationGallagherBridge.Services;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OnLocationGallagherBridge.Pages;

public class MappingModel : PageModel
{
    private readonly BridgeDbContext _db;
    private readonly IOnLocationConnector _onLocation;
    private readonly IGallagherConnector _gallagher;
    private readonly IMemoryCache _cache;
    private readonly IConfigurationStatusService _statusService;

    public MappingModel(BridgeDbContext db, IOnLocationConnector onLocation, IGallagherConnector gallagher, IMemoryCache cache, IConfigurationStatusService statusService)
    {
        _db = db;
        _onLocation = onLocation;
        _gallagher = gallagher;
        _cache = cache;
        _statusService = statusService;
    }

    [BindProperty(SupportsGet = true)]
    public string SelectedProfileId { get; set; } = string.Empty;

    public List<SelectListItem> ProfileOptions { get; set; } = new();

    public List<InductionFieldGroup> InductionFieldGroups { get; set; } = new();

    [BindProperty]
    public List<FieldMapDto> FieldMaps { get; set; } = new();

    [BindProperty]
    public List<MatchRuleDto> MatchRules { get; set; } = new();

    [BindProperty]
    public MatchRuleDto PrimaryMatch { get; set; } = new();

    [BindProperty]
    public string BridgeMessageTarget { get; set; } = string.Empty;

    [BindProperty]
    public string DefaultUnmatchedAction { get; set; } = nameof(UnmatchedAction.ManualReview);

    private string _defaultDivisionHref = string.Empty;

    [BindProperty]
    public string DefaultDivisionHref
    {
        get => _defaultDivisionHref;
        set => _defaultDivisionHref = value ?? string.Empty;
    }

    [BindProperty]
    public List<string> DefaultAccessGroupHrefs { get; set; } = new();

    public List<GallagherReference> SavedAccessGroups { get; set; } = new();
    public List<GallagherReference> DivisionOptions { get; set; } = new();
    public List<GallagherReference> AccessGroupOptions { get; set; } = new();

    public string? SourceSample { get; set; }
    public List<string> SourceFields { get; set; } = new();
    public Dictionary<string, string?> SourceFieldExamples { get; set; } = new();
    public string? GallagherSample { get; set; }
    public List<string> GallagherFields { get; set; } = new();
    public Dictionary<string, string?> GallagherFieldExamples { get; set; } = new();
    public List<string> GallagherPersonalDataFieldNames { get; set; } = new();
    public Dictionary<string, string?> GallagherPersonalDataFieldExamples { get; set; } = new();
    public List<string> GallagherCompetencyNames { get; set; } = new();
    public List<string> GallagherCompetencyFields { get; set; } = new();
    public Dictionary<string, string?> GallagherCompetencyFieldExamples { get; set; } = new();
    public string? InductionSample { get; set; }
    public List<string> SourceInductionFields { get; set; } = new();
    public Dictionary<string, string?> SourceInductionFieldExamples { get; set; } = new();
    public string? Message { get; set; }
    public ConfigurationStatus FieldMappingStatus { get; set; }

    [BindProperty]
    public string? LoadRequestId { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadProfilesAsync(ct);
        if (string.IsNullOrWhiteSpace(SelectedProfileId))
            SelectedProfileId = ProfileOptions.FirstOrDefault()?.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(SelectedProfileId)) return;

        await LoadProfilesAsync(ct);
        var profile = await LoadProfileAsync();
        FieldMappingStatus = _statusService.GetFieldMappingStatus(profile);
        LoadCachedSamples();
    }

    public async Task<IActionResult> OnPostLoadAsync(CancellationToken ct)
    {
        await LoadProfilesAsync(ct);
        var profile = await LoadProfileAsync();
        FieldMappingStatus = _statusService.GetFieldMappingStatus(profile);
        await LoadSamplesAsync(ct, LoadRequestId);
        return Page();
    }

    public IActionResult OnGetLoadStatus(string requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId)) return new JsonResult(null);
        _cache.TryGetValue(GetLoadStatusCacheKey(requestId), out MappingLoadStatus? status);
        return new JsonResult(status);
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
    {
        await LoadProfilesAsync(ct);
        var profile = await _db.SyncProfiles.FindAsync(SelectedProfileId);
        if (profile == null)
        {
            Message = "Profile not found.";
            return Page();
        }

        var fieldMaps = FieldMaps
            .Where(f => !string.IsNullOrWhiteSpace(f.Target)
                && (!string.IsNullOrWhiteSpace(f.Source)
                    || string.Equals(f.Transform, "rule-based", StringComparison.OrdinalIgnoreCase)
                    || (f.Rules ?? new List<FieldMapRuleDto>()).Any(r => !string.IsNullOrWhiteSpace(r.Source))))
            .Select(f => new FieldMapDto
            {
                Source = f.Source.Trim(),
                Target = f.Target.Trim(),
                Transform = (f.Rules ?? new List<FieldMapRuleDto>()).Any(r => !string.IsNullOrWhiteSpace(r.Source))
                    ? "rule-based"
                    : string.IsNullOrWhiteSpace(f.Transform) ? "copy" : f.Transform.Trim(),
                RuleLogic = string.IsNullOrWhiteSpace(f.RuleLogic) ? "and" : f.RuleLogic.Trim().ToLowerInvariant(),
                Rules = (f.Rules ?? new List<FieldMapRuleDto>())
                    .Where(r => !string.IsNullOrWhiteSpace(r.Source))
                    .Select(r => new FieldMapRuleDto
                    {
                        Source = r.Source.Trim(),
                        Operator = string.IsNullOrWhiteSpace(r.Operator) ? "equals" : r.Operator.Trim().ToLowerInvariant(),
                        Value = r.Value?.Trim(),
                        ValueIsSource = r.ValueIsSource
                    })
                    .ToList(),
                RuleOutputSource = f.RuleOutputSource?.Trim(),
                RuleOutputValue = f.RuleOutputValue?.Trim(),
                RuleOutputIsNumber = f.RuleOutputIsNumber
            })
            .ToList();

        var primary = new MatchRuleDto
        {
            SourceFields = JoinFields(PrimaryMatch.SourceFieldList),
            TargetFields = JoinFields(PrimaryMatch.TargetFieldList),
            MatchType = string.IsNullOrWhiteSpace(PrimaryMatch.MatchType) ? "exact" : PrimaryMatch.MatchType.Trim(),
            Priority = 0,
            IsPrimary = true
        };

        var fallbackRules = MatchRules
            .Select(r => new MatchRuleDto
            {
                SourceFields = JoinFields(r.SourceFieldList),
                TargetFields = JoinFields(r.TargetFieldList),
                MatchType = string.IsNullOrWhiteSpace(r.MatchType) ? "exact" : r.MatchType.Trim(),
                Priority = r.Priority,
                IsPrimary = false
            })
            .Where(r => !string.IsNullOrWhiteSpace(r.SourceFields) && !string.IsNullOrWhiteSpace(r.TargetFields))
            .OrderBy(r => r.Priority)
            .ToList();

        var matchRules = new List<MatchRuleDto>();
        if (!string.IsNullOrWhiteSpace(primary.SourceFields) && !string.IsNullOrWhiteSpace(primary.TargetFields))
            matchRules.Add(primary);
        matchRules.AddRange(fallbackRules);

        profile.FieldMapJson = JsonSerializer.Serialize(fieldMaps);
        profile.MatchRulesJson = JsonSerializer.Serialize(matchRules);
        SaveCreateDefaults(profile);
        profile.BridgeMessageTarget = BridgeMessageTarget ?? string.Empty;
        if (Enum.TryParse<UnmatchedAction>(DefaultUnmatchedAction, ignoreCase: true, out var unmatchedAction))
            profile.DefaultUnmatchedAction = unmatchedAction;
        await _db.SaveChangesAsync(ct);

        Message = "Mapping and match rules saved.";
        var reloadedProfile = await LoadProfileAsync();
        FieldMappingStatus = _statusService.GetFieldMappingStatus(reloadedProfile);
        LoadCachedSamples();
        return Page();
    }

    // Names are stored next to the hrefs so the selection can be displayed without calling Gallagher again.
    private void SaveCreateDefaults(SyncProfile profile)
    {
        LoadCachedSamples();

        if (string.IsNullOrWhiteSpace(DefaultDivisionHref))
        {
            profile.DefaultDivisionHref = string.Empty;
            profile.DefaultDivisionName = string.Empty;
        }
        else if (!string.Equals(profile.DefaultDivisionHref, DefaultDivisionHref, StringComparison.OrdinalIgnoreCase))
        {
            profile.DefaultDivisionHref = DefaultDivisionHref;
            profile.DefaultDivisionName = NameFor(DivisionOptions, DefaultDivisionHref) ?? string.Empty;
        }

        profile.DefaultAccessGroupsJson = JsonSerializer.Serialize(DefaultAccessGroupHrefs
            .Where(href => !string.IsNullOrWhiteSpace(href))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(href => new GallagherReference
            {
                Href = href,
                Name = NameFor(AccessGroupOptions, href) ?? NameFor(SavedAccessGroups, href) ?? href
            })
            .ToList());
    }

    private static string? NameFor(IEnumerable<GallagherReference> options, string href) => options
        .FirstOrDefault(o => string.Equals(o.Href, href, StringComparison.OrdinalIgnoreCase))?.Name;

    public IEnumerable<GallagherReference> SelectedAccessGroupsNotListed() => DefaultAccessGroupHrefs
        .Where(href => !string.IsNullOrWhiteSpace(href) && NameFor(AccessGroupOptions, href) == null)
        .Select(href => new GallagherReference { Href = href, Name = NameFor(SavedAccessGroups, href) ?? href });

    public bool DefaultDivisionIsListed => string.IsNullOrWhiteSpace(DefaultDivisionHref)
        || NameFor(DivisionOptions, DefaultDivisionHref) != null;

    private async Task LoadProfilesAsync(CancellationToken ct)
    {
        var profiles = await _db.SyncProfiles.AsNoTracking().ToListAsync(ct);
        ProfileOptions = profiles.Select(p => new SelectListItem(p.Id, p.Id, p.Id == SelectedProfileId)).ToList();
    }

    private async Task<SyncProfile?> LoadProfileAsync()
    {
        SyncProfile? profile = null;
        if (string.IsNullOrWhiteSpace(SelectedProfileId))
        {
            FieldMaps = new List<FieldMapDto>();
            MatchRules = new List<MatchRuleDto>();
            PrimaryMatch = new MatchRuleDto();
        }
        else
        {
            profile = await _db.SyncProfiles.FindAsync(SelectedProfileId);
            if (profile != null)
            {
                FieldMaps = JsonSerializer.Deserialize<List<FieldMapDto>>(profile.FieldMapJson) ?? new List<FieldMapDto>();
                foreach (var map in FieldMaps.Where(m => m.Rules.Any(r => !string.IsNullOrWhiteSpace(r.Source)) && !string.Equals(m.Transform, "rule-based", StringComparison.OrdinalIgnoreCase)))
                    map.Transform = "rule-based";
                var legacyInductionId = GetLegacyInductionId(profile.OnLocationEndpoint);
                if (!string.IsNullOrWhiteSpace(legacyInductionId))
                {
                    foreach (var map in FieldMaps.Where(m => m.Source.StartsWith("induction.", StringComparison.OrdinalIgnoreCase)))
                        map.Source = $"inductions.{legacyInductionId}.{map.Source["induction.".Length..]}";
                }
                var allRules = JsonSerializer.Deserialize<List<MatchRuleDto>>(profile.MatchRulesJson) ?? new List<MatchRuleDto>();
                PrimaryMatch = allRules.FirstOrDefault(r => r.IsPrimary) ?? new MatchRuleDto();
                MatchRules = allRules.Where(r => !r.IsPrimary).ToList();
                DefaultDivisionHref = profile.DefaultDivisionHref;
                SavedAccessGroups = TransformEngine.ParseReferences(profile.DefaultAccessGroupsJson).ToList();
                DefaultAccessGroupHrefs = SavedAccessGroups.Select(g => g.Href).ToList();
                BridgeMessageTarget = profile.BridgeMessageTarget ?? string.Empty;
                DefaultUnmatchedAction = profile.DefaultUnmatchedAction.ToString();
                if (!string.IsNullOrWhiteSpace(profile.DefaultDivisionHref) && DivisionOptions.Count == 0)
                    DivisionOptions = new List<GallagherReference> { new() { Href = profile.DefaultDivisionHref, Name = string.IsNullOrWhiteSpace(profile.DefaultDivisionName) ? profile.DefaultDivisionHref : profile.DefaultDivisionName } };
            }
        }

        while (FieldMaps.Count < 8) FieldMaps.Add(new FieldMapDto());
        foreach (var map in FieldMaps)
            while (map.Rules.Count < 10)
                map.Rules.Add(new FieldMapRuleDto());
        while (MatchRules.Count < 4) MatchRules.Add(new MatchRuleDto());

        foreach (var rule in MatchRules.Append(PrimaryMatch))
        {
            rule.SourceFieldList = SplitFields(rule.SourceFields);
            rule.TargetFieldList = SplitFields(rule.TargetFields);
        }

        return profile;
    }

    public IHtmlContent RenderSourceFieldOptions(string? selectedValue)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<option value=\"\">-- select --</option>");
        if (!string.IsNullOrWhiteSpace(selectedValue))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"<option value=\"{HtmlEncode(selectedValue)}\">{HtmlEncode(selectedValue)}</option>");
        }
        if (SourceFields.Any())
        {
            sb.AppendLine("<optgroup label=\"Profile record\">");
            foreach (var f in SourceFields)
            {
                var low = !IsPriorityProfileField(f);
                sb.AppendLine(CultureInfo.InvariantCulture, $"<option value=\"{HtmlEncode(f)}\" data-low-priority=\"{low}\">{HtmlEncode(f)}</option>");
            }
            sb.AppendLine("</optgroup>");
        }
        foreach (var induction in InductionFieldGroups)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"<optgroup label=\"{HtmlEncode($"{induction.Name} (ID {induction.Id})")}\">");
            foreach (var f in induction.Fields)
            {
                var value = $"inductions.{induction.Id}.{f}";
                var display = $"{induction.Name} (ID {induction.Id}) · {f}";
                var low = !IsPriorityInductionField(f);
                sb.AppendLine(CultureInfo.InvariantCulture, $"<option value=\"{HtmlEncode(value)}\" data-low-priority=\"{low}\">{HtmlEncode(display)}</option>");
            }
            sb.AppendLine("</optgroup>");
        }
        return new HtmlString(sb.ToString());
    }

    private static string HtmlEncode(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);

    public static List<string> SplitFields(string? value) =>
        (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    private static string JoinFields(List<string>? selected) =>
        string.Join(",", (selected ?? new List<string>())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));

    // Values saved earlier that are not in the loaded field lists still need an option, otherwise
    // saving the page would silently drop them.
    public IEnumerable<string> UnlistedSourceFields(MatchRuleDto rule)
    {
        var known = SourceFields
            .Concat(InductionFieldGroups.SelectMany(g => g.Fields.Select(f => $"inductions.{g.Id}.{f}")))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return rule.SourceFieldList.Where(v => !string.IsNullOrWhiteSpace(v) && !known.Contains(v));
    }

    public IEnumerable<string> UnlistedTargetFields(MatchRuleDto rule)
    {
        var known = GallagherFields
            .Concat(GallagherPersonalDataFieldNames.Select(f => $"personalDataFields.{f}"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return rule.TargetFieldList.Where(v => !string.IsNullOrWhiteSpace(v) && !known.Contains(v));
    }

    private async Task LoadSamplesAsync(CancellationToken ct, string? requestId = null)
    {
        if (string.IsNullOrWhiteSpace(SelectedProfileId)) return;
        SetLoadStatus(requestId, 1, "Retrieving OnLocation profile records");
        var profile = await _db.SyncProfiles.FindAsync(SelectedProfileId);
        if (profile == null) return;

        try
        {
            var bookmark = new SyncBookmark { ProfileId = profile.Id };
            var records = profile.EntityType switch
            {
                "Staff" => await _onLocation.GetStaffAsync(bookmark, ct),
                "SpMember" => await _onLocation.GetContractorMembersAsync(bookmark, ct),
                _ => new List<JsonElement>()
            };

            if (records.Count > 0)
            {
                SourceSample = records[0].ToString();
                SourceFields = records[0].EnumerateObject().Select(p => p.Name).Distinct().OrderBy(n => n).ToList();
                SourceFieldExamples = records[0].EnumerateObject()
                    .ToDictionary(p => p.Name, p => GetValue(p.Value), StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            Message = $"Could not load OnLocation sample: {ex.Message}";
        }

        try
        {
            SetLoadStatus(requestId, 2, "Retrieving OnLocation inductions and holder records");
            var bookmark = new SyncBookmark { ProfileId = profile.Id };
            var inductions = await _onLocation.GetInductionsAsync(bookmark, ct);
            foreach (var induction in inductions
                .Where(i => i.TryGetProperty("id", out var id) && id.ValueKind != JsonValueKind.Undefined)
                .OrderBy(i => GetString(i, "name") ?? GetString(i, "id")))
            {
                var id = GetString(induction, "id")!;
                var name = GetString(induction, "name") ?? $"Induction {id}";
                // Holder responses cost roughly 0.3s per record, and a single record shows every field,
                // so only one is requested per induction.
                var holders = await _onLocation.GetInductionHoldersAsync(id, new SyncBookmark { ProfileId = profile.Id }, ct, limit: 1);
                if (holders.Count == 0) continue;

                var sample = holders[0];
                InductionFieldGroups.Add(new InductionFieldGroup
                {
                    Id = id,
                    Name = name,
                    Sample = sample.ToString(),
                    Fields = sample.EnumerateObject().Select(p => p.Name).Distinct().OrderBy(n => n).ToList(),
                    Examples = sample.EnumerateObject().ToDictionary(p => p.Name, p => GetValue(p.Value), StringComparer.OrdinalIgnoreCase)
                });
            }
        }
        catch (Exception ex)
        {
            Message = $"Could not load OnLocation induction holders: {ex.Message}";
        }

        try
        {
            SetLoadStatus(requestId, 3, "Retrieving Gallagher cardholder and field definitions");
            var candidates = await _gallagher.GetCardholdersAsync(50, ct, "firstName,lastName,shortName,authorised,description,personalDataFields,competencies");
            if (candidates.Count > 0)
            {
                var sample = candidates[0];
                GallagherSample = sample.ToString();

                var pdfs = await _gallagher.GetPersonalDataFieldsAsync(100, ct);
                GallagherPersonalDataFieldNames = pdfs
                    .Select(p => p.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? NormalizeFieldName(n.GetString()) : null)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .OfType<string>()
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();
                var pdfNameSet = new HashSet<string>(GallagherPersonalDataFieldNames, StringComparer.OrdinalIgnoreCase);
                var pdfHrefToName = pdfs
                    .Where(p => p.TryGetProperty("href", out var h) && h.ValueKind == JsonValueKind.String)
                    .ToDictionary(
                        p => p.GetProperty("href").GetString()!,
                        p => (p.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? NormalizeFieldName(n.GetString()) : p.GetProperty("href").GetString())!,
                        StringComparer.OrdinalIgnoreCase);

                var comps = await _gallagher.GetCompetenciesAsync(ct);
                GallagherCompetencyNames = comps
                    .Select(p => p.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .OfType<string>()
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();

                var compHrefToName = comps
                    .Where(c => c.TryGetProperty("href", out var h) && h.ValueKind == JsonValueKind.String)
                    .ToDictionary(
                        c => c.GetProperty("href").GetString()!,
                        c => (c.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : c.GetProperty("href").GetString())!,
                        StringComparer.OrdinalIgnoreCase);

                foreach (var name in GallagherCompetencyNames)
                {
                    GallagherCompetencyFields.Add($"competencies.{name}.status");
                    GallagherCompetencyFields.Add($"competencies.{name}.expires");
                }

                // Discover PDF names/values from the sample itself as well as the definitions list
                if (sample.TryGetProperty("personalDataDefinitions", out var pdd) && pdd.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in pdd.EnumerateObject())
                    {
                        if (IsSimpleValue(prop.Value))
                        {
                            var pdfName = NormalizeFieldName(prop.Name)!;
                            pdfNameSet.Add(pdfName);
                            GallagherPersonalDataFieldExamples[pdfName] = GetValue(prop.Value) ?? "—";
                        }
                    }
                }

                if (sample.TryGetProperty("personalDataFields", out var pdfArray) && pdfArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var pdf in pdfArray.EnumerateArray())
                    {
                        var pdfName = NormalizeFieldName(GetPersonalDataFieldName(pdf, pdfHrefToName)) ?? string.Empty;
                        var pdfValue = GetString(pdf, "value") ?? GetString(pdf, "values");
                        if (!string.IsNullOrWhiteSpace(pdfName))
                        {
                            pdfNameSet.Add(pdfName);
                            if (!string.IsNullOrWhiteSpace(pdfValue))
                                GallagherPersonalDataFieldExamples[pdfName] = pdfValue;
                        }
                    }
                }

                var excludedTopLevel = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "links", "personalDataDefinitions", "personalDataFields", "competencies"
                };

                var allTopLevel = sample.EnumerateObject()
                    .Where(p => IsSimpleValue(p.Value) && !excludedTopLevel.Contains(p.Name))
                    .ToDictionary(p => NormalizeFieldName(p.Name)!, p => GetValue(p.Value), StringComparer.OrdinalIgnoreCase);

                // Any PDF that appears as a top-level scalar also belongs under Personal Data Fields
                foreach (var kvp in allTopLevel)
                {
                    if (!pdfNameSet.Contains(kvp.Key)) continue;
                    GallagherPersonalDataFieldExamples[kvp.Key] = kvp.Value ?? "—";
                }

                if (sample.TryGetProperty("competencies", out var compArray) && compArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var comp in compArray.EnumerateArray())
                    {
                        var compName = GetCompetencyName(comp, compHrefToName);
                        if (string.IsNullOrWhiteSpace(compName)) continue;
                        var status = GetString(comp, "status");
                        var expires = GetString(comp, "expires", "expiry", "expiredAt", "expirationDate");
                        if (!string.IsNullOrWhiteSpace(status))
                            GallagherCompetencyFieldExamples[$"competencies.{compName}.status"] = status;
                        if (!string.IsNullOrWhiteSpace(expires))
                            GallagherCompetencyFieldExamples[$"competencies.{compName}.expires"] = expires;
                    }
                }

                GallagherFields = allTopLevel.Keys
                    .Where(k => !pdfNameSet.Contains(k))
                    .OrderBy(n => n)
                    .ToList();
                GallagherFieldExamples = allTopLevel
                    .Where(kv => !pdfNameSet.Contains(kv.Key))
                    .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                var lastError = await _gallagher.GetLastErrorAsync();
                Message = string.IsNullOrWhiteSpace(lastError)
                    ? "Gallagher returned no cardholder records."
                    : $"Could not load Gallagher sample: {lastError}";
            }
        }
        catch (Exception ex)
        {
            Message = $"Could not load Gallagher sample: {ex.Message}";
        }

        try
        {
            SetLoadStatus(requestId, 3, "Retrieving Gallagher divisions and access groups");
            DivisionOptions = ToReferences(await _gallagher.GetDivisionsAsync(ct));
            AccessGroupOptions = ToReferences(await _gallagher.GetAccessGroupsAsync(ct));
            if (DivisionOptions.Count == 0)
            {
                var lastError = await _gallagher.GetLastErrorAsync();
                Message = string.IsNullOrWhiteSpace(lastError)
                    ? "Gallagher returned no divisions. Check that the REST operator has privileges in the division new cardholders should be created in."
                    : $"Could not load Gallagher divisions: {lastError}";
            }
        }
        catch (Exception ex)
        {
            Message = $"Could not load Gallagher divisions or access groups: {ex.Message}";
        }

        var succeeded = string.IsNullOrWhiteSpace(Message);
        SetLoadStatus(requestId, 4, succeeded ? "Field data loaded successfully" : "Field data load completed with errors", succeeded);
        if (!string.IsNullOrWhiteSpace(requestId))
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        _cache.Set(GetSampleCacheKey(), new MappingSampleCache
        {
            SourceSample = SourceSample,
            SourceFields = SourceFields,
            SourceFieldExamples = SourceFieldExamples,
            InductionFieldGroups = InductionFieldGroups,
            GallagherSample = GallagherSample,
            GallagherFields = GallagherFields,
            GallagherFieldExamples = GallagherFieldExamples,
            GallagherPersonalDataFieldNames = GallagherPersonalDataFieldNames,
            GallagherPersonalDataFieldExamples = GallagherPersonalDataFieldExamples,
            GallagherCompetencyNames = GallagherCompetencyNames,
            GallagherCompetencyFields = GallagherCompetencyFields,
            GallagherCompetencyFieldExamples = GallagherCompetencyFieldExamples,
            DivisionOptions = DivisionOptions,
            AccessGroupOptions = AccessGroupOptions
        }, TimeSpan.FromMinutes(30));
    }

    private static List<GallagherReference> ToReferences(IReadOnlyList<JsonElement> items) => items
        .Select(item => new GallagherReference
        {
            Href = GetString(item, "href") ?? string.Empty,
            Name = GetString(item, "name") ?? GetString(item, "id") ?? string.Empty
        })
        .Where(reference => !string.IsNullOrWhiteSpace(reference.Href))
        .OrderBy(reference => reference.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private void LoadCachedSamples()
    {
        if (!_cache.TryGetValue(GetSampleCacheKey(), out MappingSampleCache? cached) || cached == null) return;
        SourceSample = cached.SourceSample;
        SourceFields = cached.SourceFields;
        SourceFieldExamples = cached.SourceFieldExamples;
        InductionFieldGroups = cached.InductionFieldGroups;
        GallagherSample = cached.GallagherSample;
        GallagherFields = cached.GallagherFields;
        GallagherFieldExamples = cached.GallagherFieldExamples;
        GallagherPersonalDataFieldNames = cached.GallagherPersonalDataFieldNames;
        GallagherPersonalDataFieldExamples = cached.GallagherPersonalDataFieldExamples;
        GallagherCompetencyNames = cached.GallagherCompetencyNames;
        GallagherCompetencyFields = cached.GallagherCompetencyFields;
        GallagherCompetencyFieldExamples = cached.GallagherCompetencyFieldExamples;
        DivisionOptions = cached.DivisionOptions;
        AccessGroupOptions = cached.AccessGroupOptions;
    }

    private string GetSampleCacheKey() => $"mapping-samples:{SelectedProfileId}";

    private void SetLoadStatus(string? requestId, int step, string message, bool isSuccess = true)
    {
        if (string.IsNullOrWhiteSpace(requestId)) return;
        _cache.Set(GetLoadStatusCacheKey(requestId), new MappingLoadStatus { Step = step, Message = message, IsSuccess = isSuccess }, TimeSpan.FromMinutes(5));
    }

    private static string GetLoadStatusCacheKey(string requestId) => $"mapping-load-status:{requestId}";

    public bool IsPriorityProfileField(string field)
    {
        return new[] { "email", "id", "mobile", "name" }.Contains(field, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsPriorityInductionField(string field)
    {
        return new[] { "learner_email", "learner_from", "learner_name", "renew", "sp_member_id", "staff_id", "status" }.Contains(field, StringComparer.OrdinalIgnoreCase);
    }

    public string ExampleFor(Dictionary<string, string?> examples, string key)
    {
        if (examples.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            return value;
        return "—";
    }

    private static string? GetValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => null
        };
    }

    private static bool IsSimpleValue(JsonElement element)
    {
        return element.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null;
    }

    private static string? GetLegacyInductionId(string endpoint)
    {
        var queryStart = endpoint.IndexOf('?', StringComparison.Ordinal);
        if (queryStart < 0) return null;

        foreach (var part in endpoint[(queryStart + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && string.Equals(pair[0], "inductionId", StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(pair[1]);
        }

        return null;
    }

    private static string? NormalizeFieldName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;
        var trimmed = name.Trim();
        if (trimmed.Length > 0 && trimmed[0] == '@')
            return trimmed.Substring(1).Trim();
        return trimmed;
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
                if (prop.ValueKind == JsonValueKind.Object)
                {
                    if (prop.TryGetProperty("value", out var nestedValue) && nestedValue.ValueKind == JsonValueKind.String)
                        return nestedValue.GetString();
                    if (prop.TryGetProperty("name", out var nestedName) && nestedName.ValueKind == JsonValueKind.String)
                        return nestedName.GetString();
                }
            }
        }
        return null;
    }

    private static string? GetPersonalDataFieldName(JsonElement element, Dictionary<string, string>? hrefToName = null)
    {
        if (element.TryGetProperty("personalDataField", out var pdf) && pdf.ValueKind == JsonValueKind.Object)
        {
            if (pdf.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String) return n.GetString();
            if (hrefToName != null && pdf.TryGetProperty("href", out var h) && h.ValueKind == JsonValueKind.String && hrefToName.TryGetValue(h.GetString()!, out var mappedName))
                return mappedName;
        }
        if (element.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String) return name.GetString();
        return null;
    }

    private static string? GetCompetencyName(JsonElement element, Dictionary<string, string> hrefToName)
    {
        string? href = null;
        string? name = null;
        if (element.TryGetProperty("competency", out var comp) && comp.ValueKind == JsonValueKind.Object)
        {
            if (comp.TryGetProperty("href", out var h) && h.ValueKind == JsonValueKind.String) href = h.GetString();
            if (comp.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String) name = n.GetString();
        }
        if (element.TryGetProperty("competency", out var compStr) && compStr.ValueKind == JsonValueKind.String)
            href = compStr.GetString();
        if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(href) && hrefToName.TryGetValue(href, out var mappedName))
            name = mappedName;
        return name;
    }
}

public class InductionFieldGroup
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Sample { get; set; }
    public List<string> Fields { get; set; } = new();
    public Dictionary<string, string?> Examples { get; set; } = new();
}

public class MappingLoadStatus
{
    public int Step { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsSuccess { get; set; } = true;
}

public class MappingSampleCache
{
    public string? SourceSample { get; set; }
    public List<string> SourceFields { get; set; } = new();
    public Dictionary<string, string?> SourceFieldExamples { get; set; } = new();
    public List<InductionFieldGroup> InductionFieldGroups { get; set; } = new();
    public string? GallagherSample { get; set; }
    public List<string> GallagherFields { get; set; } = new();
    public Dictionary<string, string?> GallagherFieldExamples { get; set; } = new();
    public List<string> GallagherPersonalDataFieldNames { get; set; } = new();
    public Dictionary<string, string?> GallagherPersonalDataFieldExamples { get; set; } = new();
    public List<string> GallagherCompetencyNames { get; set; } = new();
    public List<string> GallagherCompetencyFields { get; set; } = new();
    public Dictionary<string, string?> GallagherCompetencyFieldExamples { get; set; } = new();
    public List<GallagherReference> DivisionOptions { get; set; } = new();
    public List<GallagherReference> AccessGroupOptions { get; set; } = new();
}
