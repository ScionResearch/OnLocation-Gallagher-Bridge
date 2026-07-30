using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using OnLocationGallagherBridge.Data;
using OnLocationGallagherBridge.Models;
using OnLocationGallagherBridge.Services;
using System.Text.Json;

namespace OnLocationGallagherBridge.Pages;

public class MatchReviewModel : PageModel
{
    private readonly BridgeDbContext _db;
    private readonly IOnLocationSourceService _source;
    private readonly IGallagherConnector _gallagher;
    private readonly IIdentityMatcher _matcher;
    private readonly IOnLocationConnector _onLocation;
    private readonly ITransformEngine _transform;
    private readonly IMemoryCache _cache;
    private readonly IConfigurationStatusService _statusService;
    private readonly IServiceProvider _services;

    public MatchReviewModel(BridgeDbContext db, IOnLocationSourceService source, IGallagherConnector gallagher, IIdentityMatcher matcher, IOnLocationConnector onLocation, ITransformEngine transform, IMemoryCache cache, IConfigurationStatusService statusService, IServiceProvider services)
    {
        _db = db;
        _source = source;
        _gallagher = gallagher;
        _matcher = matcher;
        _onLocation = onLocation;
        _transform = transform;
        _cache = cache;
        _statusService = statusService;
        _services = services;
    }

    [BindProperty]
    public string SelectedProfileId { get; set; } = string.Empty;

    public List<SelectListItem> ProfileOptions { get; set; } = new();

    [BindProperty]
    public List<MatchRow> Rows { get; set; } = new();

    [BindProperty]
    public int CompletedSinceDays { get; set; } = 365;

    // Model binding replaces the initialiser with null when the select posts an empty value, so the
    // setter normalises rather than relying on the initialiser.
    private string _globalResolution = string.Empty;

    [BindProperty]
    public string GlobalResolution
    {
        get => _globalResolution;
        set => _globalResolution = value ?? string.Empty;
    }

    [BindProperty]
    public string? PreviewRequestId { get; set; }

    [BindProperty]
    public string ApproveRequestId { get; set; } = string.Empty;

    [BindProperty]
    public bool BackupConfirmed { get; set; }

    public MatchPushStatus? PushStatus { get; set; }

    [BindProperty]
    public List<string> SelectedInductionIds { get; set; } = new();

    // Every induction that was rendered as a checkbox. Posted alongside the ticked ones so an empty
    // selection can be told apart from "the list was never shown".
    [BindProperty]
    public List<string> OfferedInductionIds { get; set; } = new();

    private bool InductionSelectionOffered => OfferedInductionIds.Count > 0;

    public List<InductionOption> InductionOptions { get; set; } = new();

    public class InductionOption
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int? HolderCount { get; set; }
        public bool Selected { get; set; }
    }

    public List<SelectListItem> CandidateOptions { get; set; } = new();

    public string? Message { get; set; }
    public ConfigurationStatus InitialMatchStatus { get; set; }

    public PreviewTotals? Totals { get; set; }

    public class PreviewTotals
    {
        public int OnLocationRecords { get; set; }
        public int GallagherCardholders { get; set; }
        public int Reviewable { get; set; }
        public int SkippedNoName { get; set; }
        public int AutoMatched { get; set; }
        public int NeedsConfirmation { get; set; }
        public int NoMatch { get; set; }
        public int Unresolved { get; set; }
    }

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadProfilesAsync(ct);
        InitialMatchStatus = await _statusService.GetInitialMatchStatusForAllAsync(ct);
        if (TempData["Message"] is string message) Message = message;
        var pushRequestId = Request.Query["pushRequestId"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(pushRequestId))
        {
            _cache.TryGetValue(GetApproveStatusCacheKey(pushRequestId), out MatchPushStatus? status);
            PushStatus = status;
        }
        var profileId = Request.Query["profileId"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(profileId))
        {
            var profile = await _db.SyncProfiles.FindAsync(new object?[] { profileId }, cancellationToken: ct);
            if (profile is not null)
            {
                InitialMatchStatus = _statusService.GetInitialMatchStatus(profile);
                SelectedProfileId = profileId;
            }
        }
    }

    public async Task<IActionResult> OnPostLoadInductionsAsync(CancellationToken ct)
    {
        await LoadProfilesAsync(ct);
        await LoadInductionOptionsAsync(ct);
        if (!string.IsNullOrWhiteSpace(SelectedProfileId))
        {
            var profile = await _db.SyncProfiles.FindAsync(new object?[] { SelectedProfileId }, cancellationToken: ct);
            InitialMatchStatus = _statusService.GetInitialMatchStatus(profile);
        }
        else
        {
            InitialMatchStatus = await _statusService.GetInitialMatchStatusForAllAsync(ct);
        }
        if (InductionOptions.Count == 0 && Message == null)
            Message = "This profile's field mapping does not reference any inductions, so every record from the endpoint will be considered.";
        return Page();
    }

    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken ct)
    {
        await LoadProfilesAsync(ct);
        await BuildPreviewAsync(ct);
        await LoadInductionOptionsAsync(ct);
        if (!string.IsNullOrWhiteSpace(SelectedProfileId))
        {
            var profile = await _db.SyncProfiles.FindAsync(new object?[] { SelectedProfileId }, cancellationToken: ct);
            InitialMatchStatus = _statusService.GetInitialMatchStatus(profile);
        }
        else
        {
            InitialMatchStatus = await _statusService.GetInitialMatchStatusForAllAsync(ct);
        }
        return Page();
    }

    private async Task LoadInductionOptionsAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(SelectedProfileId)) return;
        var profile = await _db.SyncProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == SelectedProfileId, ct);
        if (profile == null) return;

        var mapped = OnLocationSourceService.GetMappedInductionIds(profile.FieldMapJson);
        if (mapped.Count == 0) return;

        var selected = InductionSelectionOffered
            ? SelectedInductionIds.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : OnLocationSourceService.GetInductionIdList(profile.SelectedInductionIdsJson);
        var offered = InductionSelectionOffered;

        var details = await GetInductionDetailsAsync(ct);
        InductionOptions = mapped
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .Select(id =>
            {
                details.TryGetValue(id, out var detail);
                return new InductionOption
                {
                    Id = id,
                    Name = string.IsNullOrWhiteSpace(detail.Name) ? $"Induction {id}" : detail.Name,
                    HolderCount = detail.Holders,
                    Selected = offered ? selected.Contains(id) : selected.Count == 0 || selected.Contains(id)
                };
            })
            .ToList();
        OfferedInductionIds = InductionOptions.Select(o => o.Id).ToList();
    }

    private async Task<Dictionary<string, (string? Name, int? Holders)>> GetInductionDetailsAsync(CancellationToken ct)
    {
        const string cacheKey = "match-review-induction-details";
        if (_cache.TryGetValue(cacheKey, out Dictionary<string, (string? Name, int? Holders)>? cached) && cached is not null) return cached;

        var details = new Dictionary<string, (string? Name, int? Holders)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var inductions = await _onLocation.GetInductionsAsync(new SyncBookmark { ProfileId = "induction-names" }, ct);
            foreach (var induction in inductions)
            {
                var id = GetString(induction, "id");
                if (string.IsNullOrWhiteSpace(id)) continue;
                var holders = induction.TryGetProperty("holders", out var h) && h.ValueKind == JsonValueKind.Number ? h.GetInt32() : (int?)null;
                details[id] = (GetString(induction, "name") ?? GetString(induction, "title"), holders);
            }

            _cache.Set(cacheKey, details, TimeSpan.FromMinutes(30));
        }
        catch (Exception ex)
        {
            // Names are cosmetic; fall back to bare ids rather than blocking the selection step.
            Serilog.Log.Warning(ex, "Could not retrieve induction names from OnLocation");
        }

        return details;
    }

    public IActionResult OnGetPreviewStatus(string requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId)) return new JsonResult(null);
        _cache.TryGetValue(GetPreviewStatusCacheKey(requestId), out MatchPreviewStatus? status);
        return new JsonResult(status);
    }

    public IActionResult OnGetApproveStatus(string requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId)) return new JsonResult(null);
        _cache.TryGetValue(GetApproveStatusCacheKey(requestId), out MatchPushStatus? status);
        return new JsonResult(status);
    }

    public async Task<IActionResult> OnPostStartApproveAsync(CancellationToken ct)
    {
        await LoadProfilesAsync(ct);
        RestoreCandidateOptions();

        var profile = await _db.SyncProfiles.FindAsync(SelectedProfileId);
        if (profile == null)
            return new JsonResult(new { success = false, message = "Profile not found." });

        if (!BackupConfirmed)
            return new JsonResult(new { success = false, message = "You must confirm that Command Centre has been backed up before applying the initial match." });

        if (Rows.Count == 0 || Rows.Any(r => string.IsNullOrWhiteSpace(r.Resolution)))
            return new JsonResult(new { success = false, message = "Resolve every record as Match, Create, or Exclude before confirming the initial match." });

        if (Rows.Where(r => r.Resolution == "Match").GroupBy(r => r.CandidateHref).Any(g => string.IsNullOrWhiteSpace(g.Key) || g.Count() > 1))
            return new JsonResult(new { success = false, message = "Each matched Gallagher cardholder can only be assigned to one OnLocation record." });

        if (Rows.Any(r => r.Resolution == "Create") && string.IsNullOrWhiteSpace(profile.DefaultDivisionHref))
            return new JsonResult(new { success = false, message = "Gallagher requires a division for every new cardholder. Choose a default division in the Defaults section of the Field Mapping page before creating cardholders." });

        var requestId = string.IsNullOrWhiteSpace(ApproveRequestId) ? Guid.NewGuid().ToString("N") : ApproveRequestId;
        var profileId = SelectedProfileId;
        var rowsSnapshot = Rows.ToList();
        var cacheKey = GetApproveStatusCacheKey(requestId);
        _cache.Set(cacheKey, new MatchPushStatus { Total = rowsSnapshot.Count, Message = "Preparing to push changes to Gallagher..." }, TimeSpan.FromMinutes(30));

        _ = Task.Run(async () => await ExecuteApproveAsync(profileId, rowsSnapshot, requestId), CancellationToken.None);

        return new JsonResult(new { success = true, requestId });
    }

    private async Task ExecuteApproveAsync(string profileId, List<MatchRow> rows, string requestId)
    {
        try
        {
            await using var scope = _services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<BridgeDbContext>();
            var gallagher = scope.ServiceProvider.GetRequiredService<IGallagherConnector>();
            var transform = scope.ServiceProvider.GetRequiredService<ITransformEngine>();
            var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<MatchReviewModel>>();

            var profile = await db.SyncProfiles.FindAsync(profileId);
        if (profile == null)
        {
            cache.Set(GetApproveStatusCacheKey(requestId), new MatchPushStatus
            {
                IsComplete = true,
                IsSuccess = false,
                Message = "Profile not found."
            }, TimeSpan.FromMinutes(30));
            return;
        }

        var pushStatus = new MatchPushStatus { Total = rows.Count, Message = "Preparing to push changes to Gallagher..." };
        var cacheKey = GetApproveStatusCacheKey(requestId);
        var errors = new List<string>();

        void Report(string message, string? detail = null)
        {
            pushStatus.Message = message;
            pushStatus.Detail = detail ?? pushStatus.Detail;
            cache.Set(cacheKey, pushStatus, TimeSpan.FromMinutes(30));
        }

        var correlationId = Guid.NewGuid().ToString("N");
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            pushStatus.Processed = index + 1;
            Report($"Processing {row.SourceDisplay}...", $"{pushStatus.Processed} of {pushStatus.Total}");

            var mapping = await db.EntityMappings.FirstOrDefaultAsync(
                    m => m.ProfileId == profile.Id && m.SourceType == profile.EntityType && m.SourceId == row.SourceId)
                ?? new EntityMapping { ProfileId = profile.Id, SourceType = profile.EntityType, SourceId = row.SourceId };

            try
            {
                if (row.Resolution == "Create")
                {
                    using var sourceDocument = JsonDocument.Parse(row.SourceJson);
                    var transformed = transform.Transform(profile, sourceDocument.RootElement);
                    var payload = transform.ApplyCreateDefaults(profile, await transform.BuildCardholderPayloadAsync(transformed, null, CancellationToken.None));
                    var created = await gallagher.CreateCardholderAsync(payload, CancellationToken.None);
                    if (!created.HasValue)
                    {
                        var error = await gallagher.GetLastErrorAsync();
                        throw new InvalidOperationException($"Could not create cardholder for {row.SourceDisplay}: {error}");
                    }
                    mapping.GallagherHref = GetString(created.Value, "href") ?? string.Empty;
                    mapping.GallagherId = GetString(created.Value, "id");
                    mapping.Confidence = 1;
                    mapping.ManualOverride = true;
                    mapping.Excluded = false;
                    pushStatus.Created++;
                }
                else if (row.Resolution == "Exclude")
                {
                    mapping.GallagherHref = string.Empty;
                    mapping.GallagherId = null;
                    mapping.Confidence = 0;
                    mapping.ManualOverride = true;
                    mapping.Excluded = true;
                    pushStatus.Excluded++;
                }
                else
                {
                    mapping.GallagherHref = row.CandidateHref ?? string.Empty;
                    mapping.GallagherId = GetIdFromHref(row.CandidateHref) ?? row.CandidateId;
                    mapping.Confidence = row.Confidence;
                    mapping.ManualOverride = true;
                    mapping.Excluded = false;
                    pushStatus.Matched++;
                }
            }
            catch (Exception ex)
            {
                pushStatus.Failed++;
                errors.Add(ex.Message);
                Report($"Failed on {row.SourceDisplay}", ex.Message);
                logger.LogError(ex, "Failed to apply initial match for {Source}", row.SourceId);
                continue;
            }

            mapping.UpdatedAt = DateTimeOffset.UtcNow;
            if (db.Entry(mapping).State == EntityState.Detached) db.EntityMappings.Add(mapping);

            if (row.Resolution != "Exclude" && !string.IsNullOrWhiteSpace(row.SourceId) && !string.IsNullOrWhiteSpace(row.SourceJson))
            {
                var pending = await db.SyncJobs.FirstOrDefaultAsync(
                    j => j.ProfileId == profile.Id && j.SourceId == row.SourceId && j.Status == "Pending");
                if (pending != null)
                {
                    pending.PayloadJson = row.SourceJson;
                    pending.UpdatedAt = DateTimeOffset.UtcNow;
                }
                else
                {
                    db.SyncJobs.Add(new SyncJob
                    {
                        ProfileId = profile.Id,
                        SourceType = profile.EntityType,
                        SourceId = row.SourceId,
                        PayloadJson = row.SourceJson,
                        Status = "Pending",
                        CorrelationId = correlationId
                    });
                }
            }
        }

        profile.InitialMatchCompleted = true;
        profile.InitialMatchCompletedAt = DateTimeOffset.UtcNow;
        profile.Enabled = true;
        profile.NextRun = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(CancellationToken.None);

        pushStatus.IsComplete = true;
        pushStatus.IsSuccess = pushStatus.Failed == 0;
        pushStatus.Errors = errors.Take(20).ToList();
        if (pushStatus.IsSuccess)
        {
            pushStatus.Message = "Initial match applied successfully.";
            pushStatus.Detail = $"{pushStatus.Matched} matched, {pushStatus.Created} created, {pushStatus.Excluded} excluded. Automatic sync is now enabled and queued jobs will push mapped fields/competencies.";
        }
        else
        {
            pushStatus.Message = $"Initial match completed with {pushStatus.Failed} failure(s).";
            pushStatus.Detail = $"{pushStatus.Matched} matched, {pushStatus.Created} created, {pushStatus.Excluded} excluded, {pushStatus.Failed} failed. Fix the reported problem(s) and confirm again; rows already applied have been saved.";
        }
        cache.Set(cacheKey, pushStatus, TimeSpan.FromMinutes(30));
        cache.Remove(GetCandidateOptionsCacheKey(profile.Id));
        }
        catch (Exception ex)
        {
            _cache.Set(GetApproveStatusCacheKey(requestId), new MatchPushStatus
            {
                IsComplete = true,
                IsSuccess = false,
                Message = "Initial match push failed unexpectedly.",
                Detail = ex.Message
            }, TimeSpan.FromMinutes(30));
        }
    }

    private static string? GetIdFromHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href)) return null;
        var segment = href.TrimEnd('/').Split('/').LastOrDefault();
        return string.IsNullOrWhiteSpace(segment) ? null : segment;
    }

    private static string GetCandidateOptionsCacheKey(string profileId) => $"match-review-candidates:{profileId}";

    private void RestoreCandidateOptions()
    {
        if (string.IsNullOrWhiteSpace(SelectedProfileId)) return;
        if (_cache.TryGetValue(GetCandidateOptionsCacheKey(SelectedProfileId), out List<SelectListItem>? cached) && cached is not null)
            CandidateOptions = cached;
    }

    private async Task LoadProfilesAsync(CancellationToken ct)
    {
        var profiles = await _db.SyncProfiles.AsNoTracking().ToListAsync(ct);
        ProfileOptions = profiles.Select(p => new SelectListItem(p.Id, p.Id, p.Id == SelectedProfileId)).ToList();
    }

    private async Task BuildPreviewAsync(CancellationToken ct)
    {
        Rows.Clear();
        if (string.IsNullOrWhiteSpace(SelectedProfileId)) return;
        var profile = await _db.SyncProfiles.FindAsync(SelectedProfileId);
        if (profile == null) return;

        if (!HasRequiredMapping(profile))
        {
            Message = "Add at least one field mapping and a primary match rule before starting Initial Record Match.";
            return;
        }

        var mappedInductions = OnLocationSourceService.GetMappedInductionIds(profile.FieldMapJson);
        if (InductionSelectionOffered && mappedInductions.Count > 0)
        {
            var chosen = SelectedInductionIds.Where(id => mappedInductions.Contains(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (chosen.Count == 0)
            {
                Message = "Select at least one induction to pull through before building the review.";
                SetPreviewStatus(PreviewRequestId, 4, Message, false);
                return;
            }

            var tracked = await _db.SyncProfiles.FindAsync(new object?[] { SelectedProfileId }, ct);
            if (tracked != null)
            {
                tracked.SelectedInductionIdsJson = JsonSerializer.Serialize(chosen);
                await _db.SaveChangesAsync(ct);
                profile = tracked;
            }
        }
        SetPreviewStatus(PreviewRequestId, 1, "Checking the OnLocation and Gallagher connections", true, 0);
        if (!await _onLocation.TestConnectionAsync(ct) || !await _gallagher.TestConnectionAsync(ct))
        {
            Message = "Initial Record Match preflight failed. Check the Connector Settings and that the configured credentials are correct.";
            SetPreviewStatus(PreviewRequestId, 4, Message, false);
            return;
        }

        IReadOnlyList<JsonElement> records;
        IReadOnlyList<JsonElement> candidates;
        try
        {
            SetPreviewStatus(PreviewRequestId, 2, "Retrieving OnLocation records and induction history", true, 5);
            var onLocationProgress = new Progress<OnLocationFetchProgress>(p =>
            {
                var overallPercent = 5 + (int)(p.Percent * 0.75); // 5-80%
                SetPreviewStatus(PreviewRequestId, 2, "Retrieving OnLocation records and induction history", true, overallPercent, p.Message);
            });
            records = await _source.GetInitialMatchRecordsAsync(profile, DateTimeOffset.UtcNow.AddDays(-CompletedSinceDays), onLocationProgress, ct);
            if (records.Count == 0)
            {
                var onLocationError = await _onLocation.GetLastErrorAsync();
                Message = string.IsNullOrWhiteSpace(onLocationError)
                    ? $"No OnLocation records have an induction completed in the last {CompletedSinceDays} days."
                    : $"Could not load OnLocation records: {onLocationError}";
                SetPreviewStatus(PreviewRequestId, 4, Message, false);
                return;
            }

            SetPreviewStatus(PreviewRequestId, 3, "Retrieving Gallagher cardholders and calculating matches", true, 80);
            candidates = await _gallagher.GetAllCardholdersAsync(ct, BuildCandidateFieldSpecifier(profile));
            if (candidates.Count == 0) candidates = await _gallagher.GetAllCardholdersAsync(ct);
            if (candidates.Count == 0)
            {
                var lastError = await _gallagher.GetLastErrorAsync();
                Message = string.IsNullOrWhiteSpace(lastError)
                    ? "Gallagher returned no cardholder records."
                    : $"Could not load Gallagher candidates: {lastError}";
                SetPreviewStatus(PreviewRequestId, 4, Message, false);
                return;
            }
        }
        catch (Exception ex)
        {
            Message = $"Preview failed: {ex.Message}";
            SetPreviewStatus(PreviewRequestId, 4, Message, false);
            return;
        }

        var session = _matcher.CreateSession(profile, candidates);
        var automaticMatches = records
            .Where(HasName)
            .Select(record => (Record: record, Match: session.Match(record)))
            .ToList();

        var automaticallyAssignedHrefs = automaticMatches
            .Where(x => x.Match is { RequiresManualReview: false })
            .Select(x => x.Match!.GallagherHref)
            .Where(href => !string.IsNullOrWhiteSpace(href))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        CandidateOptions = candidates
            .Where(c => !automaticallyAssignedHrefs.Contains(GetString(c, "href") ?? string.Empty))
            .Select(c => new SelectListItem(GetDisplay(c), GetString(c, "href")))
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .OrderBy(x => x.Text)
            .ToList();
        _cache.Set(GetCandidateOptionsCacheKey(profile.Id), CandidateOptions, TimeSpan.FromHours(4));

        var candidatesByHref = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            var href = GetString(candidate, "href");
            if (!string.IsNullOrWhiteSpace(href)) candidatesByHref[href] = candidate;
        }

        foreach (var (record, match) in automaticMatches)
        {
            var requiresReview = match?.RequiresManualReview ?? false;
            string? candidateDisplay = "No automatic match";
            string? candidateId = null;
            if (match?.GallagherHref is not null)
            {
                if (candidatesByHref.TryGetValue(match.GallagherHref, out var candidate))
                {
                    candidateDisplay = GetDisplay(candidate);
                    candidateId = GetString(candidate, "id");
                }
                else
                {
                    candidateDisplay = "Matched cardholder (details unavailable)";
                }
            }

            var autoMatched = match?.GallagherHref is not null && !requiresReview;
            Rows.Add(new MatchRow
            {
                SourceId = GetString(record, "id") ?? string.Empty,
                SourceDisplay = GetDisplay(record),
                SourceJson = record.ToString() ?? "{}",
                SourceProperties = FlattenProperties(record),
                CandidateHref = match?.GallagherHref,
                CandidateDisplay = candidateDisplay,
                CandidateId = candidateId,
                Confidence = match?.Confidence ?? 0,
                Reason = match?.Reason ?? "No match found",
                RequiresReview = requiresReview,
                Resolution = autoMatched ? "Match" : requiresReview ? string.Empty : GlobalResolution,
                Approved = false
            });
        }

        Totals = new PreviewTotals
        {
            OnLocationRecords = records.Count,
            GallagherCardholders = candidates.Count,
            Reviewable = Rows.Count,
            SkippedNoName = records.Count - Rows.Count,
            AutoMatched = Rows.Count(r => r.CandidateHref is not null && !r.RequiresReview),
            NeedsConfirmation = Rows.Count(r => r.RequiresReview),
            NoMatch = Rows.Count(r => r.CandidateHref is null),
            Unresolved = Rows.Count(r => string.IsNullOrWhiteSpace(r.Resolution))
        };

        if (Rows.Count == 0)
        {
            Message = $"{records.Count} OnLocation record(s) were retrieved but none had a usable name to review.";
            SetPreviewStatus(PreviewRequestId, 4, Message, false, 100);
            return;
        }

        SetPreviewStatus(PreviewRequestId, 4, $"Review ready: {Rows.Count} named records loaded, {Totals.Unresolved} need a decision", true, 100);
    }

    private static string BuildCandidateFieldSpecifier(SyncProfile profile)
    {
        var rules = JsonSerializer.Deserialize<List<MatchRuleDto>>(profile.MatchRulesJson) ?? new List<MatchRuleDto>();
        var fields = new List<string> { "defaults" };
        foreach (var target in rules.SelectMany(r => r.TargetFields.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            var root = target.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(root) && !fields.Contains(root, StringComparer.OrdinalIgnoreCase)) fields.Add(root);
        }
        return string.Join(',', fields);
    }

    private static bool HasRequiredMapping(SyncProfile profile)
    {
        var maps = JsonSerializer.Deserialize<List<FieldMapDto>>(profile.FieldMapJson) ?? new List<FieldMapDto>();
        var rules = JsonSerializer.Deserialize<List<MatchRuleDto>>(profile.MatchRulesJson) ?? new List<MatchRuleDto>();
        return maps.Any(m => !string.IsNullOrWhiteSpace(m.Source) && !string.IsNullOrWhiteSpace(m.Target))
            && rules.Any(r => r.IsPrimary && !string.IsNullOrWhiteSpace(r.SourceFields) && !string.IsNullOrWhiteSpace(r.TargetFields));
    }

    private static List<MatchProperty> FlattenProperties(JsonElement record)
    {
        var properties = new List<MatchProperty>();
        AddProperties(properties, record, string.Empty);
        return properties;
    }

    private static void AddProperties(List<MatchProperty> properties, JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                AddProperties(properties, property.Value, string.IsNullOrEmpty(path) ? property.Name : $"{path}.{property.Name}");
            }
            return;
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                AddProperties(properties, item, $"{path}[{index++}]");
            }
            return;
        }

        if (!string.IsNullOrEmpty(path))
        {
            properties.Add(new MatchProperty
            {
                Name = path,
                Value = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText()
            });
        }
    }

    private static bool HasName(JsonElement record) => !string.IsNullOrWhiteSpace(GetName(record));

    private static string GetDisplay(JsonElement record) => GetName(record) ?? "Unnamed record";

    private static string? GetName(JsonElement record)
    {
        var first = GetString(record, "first_name", "firstName", "firstname");
        var last = GetString(record, "last_name", "lastName", "lastname");
        var name = $"{first} {last}".Trim();
        return !string.IsNullOrWhiteSpace(name) ? name : GetString(record, "name", "full_name", "fullName", "shortName");
    }

    private void SetPreviewStatus(string? requestId, int step, string message, bool isSuccess = true, int? progressPercent = null, string? detail = null)
    {
        if (string.IsNullOrWhiteSpace(requestId)) return;
        _cache.Set(GetPreviewStatusCacheKey(requestId), new MatchPreviewStatus { Step = step, Message = message, IsSuccess = isSuccess, ProgressPercent = progressPercent, Detail = detail }, TimeSpan.FromMinutes(15));
    }

    private static string GetPreviewStatusCacheKey(string requestId) => $"match-review-preview-status:{requestId}";

    private static string GetApproveStatusCacheKey(string requestId) => $"match-review-approve-status:{requestId}";

    private static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.String) return prop.GetString();
                if (prop.ValueKind == JsonValueKind.Number) return prop.GetRawText();
            }
        }
        return null;
    }

    public class MatchRow
    {
        public string SourceId { get; set; } = string.Empty;
        public string SourceDisplay { get; set; } = string.Empty;
        public string SourceJson { get; set; } = "{}";
        public List<MatchProperty> SourceProperties { get; set; } = new();
        public string? CandidateHref { get; set; }
        public string? CandidateId { get; set; }
        public string CandidateDisplay { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string Reason { get; set; } = string.Empty;
        public bool Approved { get; set; }
        public bool RequiresReview { get; set; }
        private string _resolution = string.Empty;

        public string Resolution
        {
            get => _resolution;
            set => _resolution = value ?? string.Empty;
        }
    }

    public class MatchProperty
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class MatchPreviewStatus
    {
        public int Step { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsSuccess { get; set; } = true;
        public int? ProgressPercent { get; set; }
        public string? Detail { get; set; }
    }

    public class MatchPushStatus
    {
        public int Total { get; set; }
        public int Processed { get; set; }
        public int Matched { get; set; }
        public int Created { get; set; }
        public int Excluded { get; set; }
        public int Failed { get; set; }
        public bool IsComplete { get; set; }
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
    }
}
