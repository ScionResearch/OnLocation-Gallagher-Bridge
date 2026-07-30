using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OnLocationGallagherBridge.Data;
using OnLocationGallagherBridge.Models;
using OnLocationGallagherBridge.Services;
using System.Text.Json;

namespace OnLocationGallagherBridge.Pages;

public class ExceptionsModel : PageModel
{
    private readonly BridgeDbContext _db;
    private readonly IIdentityMatcher _matcher;
    private readonly IGallagherConnector _gallagher;
    private readonly IAuditService _audit;
    private readonly Serilog.ILogger _logger;
    private readonly IConfigurationStatusService _statusService;

    public ExceptionsModel(BridgeDbContext db, IIdentityMatcher matcher, IGallagherConnector gallagher, IAuditService audit, IConfigurationStatusService statusService)
    {
        _db = db;
        _matcher = matcher;
        _gallagher = gallagher;
        _audit = audit;
        _statusService = statusService;
        _logger = Serilog.Log.Logger.ForContext<ExceptionsModel>();
    }

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var status = await _statusService.GetOverallStateAsync(false, context.HttpContext.RequestAborted);
        if (status.Overall != ConfigurationStatus.Complete)
        {
            context.Result = new RedirectToPageResult("/Setup");
            return;
        }
        await next();
    }

    public class FailureRow
    {
        public Guid JobId { get; set; }
        public string ProfileId { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public string Display { get; set; } = string.Empty;
        public string Attempted { get; set; } = string.Empty;
        public string? Error { get; set; }
        public string? Advice { get; set; }
        public string? GallagherHref { get; set; }
        public int RetryCount { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string PayloadJson { get; set; } = "{}";
    }

    public class ErrorGroup
    {
        public string Summary { get; set; } = string.Empty;
        public string Advice { get; set; } = string.Empty;
        public int Count { get; set; }
        public List<FailureRow> Rows { get; set; } = new();
    }

    public class ManualMatchRow
    {
        public Guid QueueId { get; set; }
        public string ProfileId { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public string Display { get; set; } = string.Empty;
        public string SourceJson { get; set; } = "{}";
        public string? CandidateHref { get; set; }
        public string? CandidateDisplay { get; set; }
        public double Confidence { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> CandidateOptions { get; set; } = new();
    }

    public List<ErrorGroup> Groups { get; set; } = new();
    public List<ManualMatchRow> ManualMatches { get; set; } = new();
    public int PendingManualMatches { get; set; }
    public int StaleLinks { get; set; }
    public string? Message { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        if (TempData["Message"] is string message) Message = message;
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        // SQLite cannot ORDER BY a DateTimeOffset, so the sort has to happen after materialising.
        var failedJobs = await _db.SyncJobs.AsNoTracking()
            .Where(j => j.Status == "Failed")
            .ToListAsync(ct);
        var failed = failedJobs.OrderByDescending(j => j.UpdatedAt).Take(500).ToList();

        var mappings = await _db.EntityMappings.AsNoTracking().ToListAsync(ct);
        var rows = new List<FailureRow>();
        foreach (var job in failed)
        {
            var mapping = mappings.FirstOrDefault(m => m.ProfileId == job.ProfileId && m.SourceId == job.SourceId);
            rows.Add(new FailureRow
            {
                JobId = job.Id,
                ProfileId = job.ProfileId,
                SourceId = job.SourceId,
                Display = Describe(job.PayloadJson, job.SourceId),
                Attempted = string.IsNullOrEmpty(mapping?.GallagherHref) ? "Create cardholder" : "Update cardholder",
                Error = job.Error,
                Advice = Advise(job.Error),
                GallagherHref = mapping?.GallagherHref,
                RetryCount = job.RetryCount,
                UpdatedAt = job.UpdatedAt,
                PayloadJson = Prettify(job.PayloadJson)
            });
        }

        // Identical errors are grouped so one systemic problem reads as one problem rather than 34.
        Groups = rows
            .GroupBy(r => Normalise(r.Error))
            .Select(g => new ErrorGroup
            {
                Summary = g.Key,
                Advice = g.First().Advice ?? "",
                Count = g.Count(),
                Rows = g.OrderByDescending(r => r.UpdatedAt).ToList()
            })
            .OrderByDescending(g => g.Count)
            .ToList();

        PendingManualMatches = await _db.ManualMatchQueues.CountAsync(m => m.Status == "Pending", ct);
        StaleLinks = await _db.AuditLogs.CountAsync(a => a.Action == "StaleLink", ct);
        await LoadManualMatchesAsync(ct);
    }

    public async Task<IActionResult> OnPostRetryAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _db.SyncJobs.FindAsync(new object?[] { jobId }, cancellationToken: ct);
        if (job == null)
        {
            TempData["Message"] = "That job no longer exists.";
            return RedirectToPage();
        }

        job.Status = "Pending";
        job.Error = null;
        job.RetryCount++;
        job.UpdatedAt = DateTimeOffset.UtcNow;
        await BringProfileForwardAsync(job.ProfileId, ct);
        await _db.SaveChangesAsync(ct);

        TempData["Message"] = $"Queued {job.SourceId} for another attempt.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRetryGroupAsync(string summary, CancellationToken ct)
    {
        var failed = await _db.SyncJobs.Where(j => j.Status == "Failed").ToListAsync(ct);
        var affected = failed.Where(j => Normalise(j.Error) == summary).ToList();
        foreach (var job in affected)
        {
            job.Status = "Pending";
            job.Error = null;
            job.RetryCount++;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await BringProfileForwardAsync(job.ProfileId, ct);
        }
        await _db.SaveChangesAsync(ct);

        TempData["Message"] = $"Queued {affected.Count} record(s) for another attempt.";
        return RedirectToPage();
    }

    // A failure caused by a broken link cannot be fixed by retrying, so the link is dropped and the record is
    // sent back for matching.
    public async Task<IActionResult> OnPostRematchAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _db.SyncJobs.FindAsync(new object?[] { jobId }, cancellationToken: ct);
        if (job == null)
        {
            TempData["Message"] = "That job no longer exists.";
            return RedirectToPage();
        }

        var mapping = await _db.EntityMappings
            .FirstOrDefaultAsync(m => m.ProfileId == job.ProfileId && m.SourceId == job.SourceId, ct);
        if (mapping != null)
        {
            mapping.GallagherHref = string.Empty;
            mapping.GallagherId = null;
            mapping.ManualOverride = false;
            mapping.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var queued = await _db.ManualMatchQueues
            .FirstOrDefaultAsync(m => m.ProfileId == job.ProfileId && m.SourceId == job.SourceId && m.Status == "Pending", ct);
        if (queued == null)
        {
            _db.ManualMatchQueues.Add(new ManualMatchQueue
            {
                ProfileId = job.ProfileId,
                SourceId = job.SourceId,
                SourceJson = job.PayloadJson,
                Status = "Pending"
            });
        }

        job.Status = "ManualReview";
        job.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        TempData["Message"] = $"Unlinked {job.SourceId} and sent it back for matching.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDismissAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _db.SyncJobs.FindAsync(new object?[] { jobId }, cancellationToken: ct);
        if (job == null)
        {
            TempData["Message"] = "That job no longer exists.";
            return RedirectToPage();
        }

        job.Status = "Dismissed";
        job.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.Information("Failure for {Source} on profile {Profile} was dismissed by an operator", job.SourceId, job.ProfileId);

        TempData["Message"] = $"Dismissed the failure for {job.SourceId}. It will be retried the next time the record changes in OnLocation.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDismissAllAsync(CancellationToken ct)
    {
        var failed = await _db.SyncJobs.Where(j => j.Status == "Failed").ToListAsync(ct);
        foreach (var job in failed)
        {
            job.Status = "Dismissed";
            job.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync(ct);

        TempData["Message"] = $"Dismissed {failed.Count} failure(s).";
        return RedirectToPage();
    }

    private async Task BringProfileForwardAsync(string profileId, CancellationToken ct)
    {
        var profile = await _db.SyncProfiles.FindAsync(new object?[] { profileId }, cancellationToken: ct);
        if (profile != null) profile.NextRun = DateTimeOffset.UtcNow;
    }

    private async Task LoadManualMatchesAsync(CancellationToken ct)
    {
        var queueItems = (await _db.ManualMatchQueues
            .Where(m => m.Status == "Pending")
            .ToListAsync(ct))
            .OrderByDescending(m => m.CreatedAt)
            .ToList();

        var profiles = await _db.SyncProfiles.AsNoTracking().ToListAsync(ct);
        var profileMap = profiles.ToDictionary(p => p.Id);

        ManualMatches = new List<ManualMatchRow>();
        var candidatesByProfile = new Dictionary<string, IReadOnlyList<JsonElement>>();

        foreach (var item in queueItems)
        {
            if (!profileMap.TryGetValue(item.ProfileId, out var profile)) continue;

            JsonElement source;
            try
            {
                using var doc = JsonDocument.Parse(item.SourceJson);
                source = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                continue;
            }

            if (!candidatesByProfile.TryGetValue(profile.Id, out var candidates))
            {
                candidates = await _gallagher.GetCardholdersAsync(250, ct, BuildCandidateFieldSpecifier(profile));
                if (candidates.Count == 0) candidates = await _gallagher.GetCardholdersAsync(250, ct);
                candidatesByProfile[profile.Id] = candidates;
            }

            var match = await _matcher.FindBestMatchAsync(profile, source, candidates, ct);
            var candidateHref = item.CandidateHref ?? match?.GallagherHref;
            var candidateDisplay = candidateHref is null ? null : GetDisplay(candidates.FirstOrDefault(c => string.Equals(GetString(c, "href") ?? string.Empty, candidateHref, StringComparison.OrdinalIgnoreCase)));

            var options = candidates
                .Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Text = GetDisplay(c),
                    Value = GetString(c, "href") ?? string.Empty,
                    Selected = candidateHref is not null && string.Equals(GetString(c, "href") ?? string.Empty, candidateHref, StringComparison.OrdinalIgnoreCase)
                })
                .Where(o => !string.IsNullOrWhiteSpace(o.Value))
                .OrderBy(o => o.Text)
                .ToList();

            ManualMatches.Add(new ManualMatchRow
            {
                QueueId = item.Id,
                ProfileId = item.ProfileId,
                SourceId = item.SourceId,
                Display = Describe(item.SourceJson, item.SourceId),
                SourceJson = Prettify(item.SourceJson),
                CandidateHref = candidateHref,
                CandidateDisplay = candidateDisplay,
                Confidence = match?.Confidence ?? 0,
                Reason = match?.Reason ?? (candidateHref is null ? "No match found" : "Suggested match"),
                CandidateOptions = options
            });
        }
    }

    public async Task<IActionResult> OnPostResolveCreateAsync(Guid queueId, CancellationToken ct)
    {
        var (queue, profile, job) = await ResolveQueueItem(queueId, ct);
        if (queue == null) return RedirectToPage();
        if (profile == null)
        {
            TempData["Message"] = "Profile not found.";
            return RedirectToPage();
        }

        var mapping = await FindOrAddMappingAsync(profile, queue.SourceId, ct);
        mapping.GallagherHref = string.Empty;
        mapping.GallagherId = null;
        mapping.ManualOverride = true;
        mapping.Excluded = false;
        mapping.UpdatedAt = DateTimeOffset.UtcNow;

        await FinaliseResolutionAsync(queue, job, mapping, "Pending", ct);
        TempData["Message"] = $"{queue.SourceId} will be created as a new cardholder on the next sync.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResolveMatchAsync(Guid queueId, string candidateHref, CancellationToken ct)
    {
        var (queue, profile, job) = await ResolveQueueItem(queueId, ct);
        if (queue == null) return RedirectToPage();
        if (profile == null)
        {
            TempData["Message"] = "Profile not found.";
            return RedirectToPage();
        }
        if (string.IsNullOrWhiteSpace(candidateHref))
        {
            TempData["Message"] = "Please select a cardholder to match.";
            return RedirectToPage();
        }

        var mapping = await FindOrAddMappingAsync(profile, queue.SourceId, ct);
        mapping.GallagherHref = candidateHref;
        mapping.ManualOverride = true;
        mapping.Excluded = false;
        mapping.UpdatedAt = DateTimeOffset.UtcNow;

        await FinaliseResolutionAsync(queue, job, mapping, "Pending", ct);
        TempData["Message"] = $"{queue.SourceId} matched to the selected cardholder.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResolveIgnoreAsync(Guid queueId, CancellationToken ct)
    {
        var (queue, profile, job) = await ResolveQueueItem(queueId, ct);
        if (queue == null) return RedirectToPage();
        if (profile == null)
        {
            TempData["Message"] = "Profile not found.";
            return RedirectToPage();
        }

        var mapping = await FindOrAddMappingAsync(profile, queue.SourceId, ct);
        mapping.GallagherHref = string.Empty;
        mapping.GallagherId = null;
        mapping.ManualOverride = false;
        mapping.Excluded = true;
        mapping.UpdatedAt = DateTimeOffset.UtcNow;

        await FinaliseResolutionAsync(queue, job, mapping, "Complete", ct);
        TempData["Message"] = $"{queue.SourceId} will be ignored.";
        return RedirectToPage();
    }

    private async Task<(ManualMatchQueue? Queue, SyncProfile? Profile, SyncJob? Job)> ResolveQueueItem(Guid queueId, CancellationToken ct)
    {
        var queue = await _db.ManualMatchQueues.FindAsync(new object?[] { queueId }, cancellationToken: ct);
        if (queue == null)
        {
            TempData["Message"] = "That record is no longer waiting for a match.";
            return (null, null, null);
        }

        var profile = await _db.SyncProfiles.FindAsync(new object?[] { queue.ProfileId }, cancellationToken: ct);
        if (profile == null)
        {
            TempData["Message"] = "The profile for that record no longer exists.";
            return (null, null, null);
        }

        var job = await _db.SyncJobs
            .FirstOrDefaultAsync(j => j.ProfileId == queue.ProfileId && j.SourceId == queue.SourceId && j.Status == "ManualReview", ct);

        return (queue, profile, job);
    }

    private async Task<EntityMapping> FindOrAddMappingAsync(SyncProfile profile, string sourceId, CancellationToken ct)
    {
        var mapping = await _db.EntityMappings
            .FirstOrDefaultAsync(m => m.ProfileId == profile.Id && m.SourceType == profile.EntityType && m.SourceId == sourceId, ct);
        if (mapping != null) return mapping;

        mapping = new EntityMapping
        {
            ProfileId = profile.Id,
            SourceType = profile.EntityType,
            SourceId = sourceId,
            GallagherHref = string.Empty,
            ManualOverride = false
        };
        _db.EntityMappings.Add(mapping);
        return mapping;
    }

    private async Task FinaliseResolutionAsync(ManualMatchQueue queue, SyncJob? job, EntityMapping mapping, string jobStatus, CancellationToken ct)
    {
        queue.Status = "Approved";
        queue.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (job != null)
        {
            job.Status = jobStatus;
            job.Error = null;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            await BringProfileForwardAsync(job.ProfileId, ct);
            await _db.SaveChangesAsync(ct);
        }
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

    private static string GetDisplay(JsonElement candidate)
    {
        var first = GetString(candidate, "firstName");
        var last = GetString(candidate, "lastName");
        var name = $"{first} {last}".Trim();
        if (string.IsNullOrWhiteSpace(name)) name = GetString(candidate, "shortName") ?? GetString(candidate, "name") ?? string.Empty;
        var email = GetString(candidate, "email");
        if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(name)) return $"{name} ({email})";
        return !string.IsNullOrWhiteSpace(name) ? name : email ?? "Unknown cardholder";
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

    private static string Normalise(string? error)
    {
        if (string.IsNullOrWhiteSpace(error)) return "Unknown error";
        // Strip the record-specific href so the same class of failure groups together.
        var text = System.Text.RegularExpressions.Regex.Replace(error, @"https?://\S+", "<cardholder>");
        return text.Length <= 200 ? text : text[..200];
    }

    private static string? Advise(string? error)
    {
        if (string.IsNullOrWhiteSpace(error)) return null;
        if (error.Contains("404"))
            return "The linked cardholder is gone from Command Centre, or the REST operator cannot see its division. Use Unlink and re-match, then pick the correct cardholder on the Initial Record Match page.";
        if (error.Contains("401") || error.Contains("403"))
            return "Command Centre rejected the credentials or the operator lacks privilege for this division. Check the API key and the REST operator's privileges under Connector Settings.";
        if (error.Contains("400") || error.Contains("422"))
            return "Command Centre rejected the payload. Check the Field Mapping targets, especially personal data field names and competency expiry values.";
        if (error.Contains("409"))
            return "Command Centre reported a conflict, usually a duplicate value on a unique personal data field such as email.";
        if (error.Contains("timed out") || error.Contains("timeout") || error.Contains("connection"))
            return "Command Centre could not be reached. Confirm the server is running and reachable, then retry.";
        return "Retry once the underlying problem is resolved. The full request payload is shown below.";
    }

    private static string Describe(string payloadJson, string fallback)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            foreach (var name in new[] { "name", "email" })
            {
                if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    var text = value.GetString();
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
            }
            if (root.TryGetProperty("first_name", out var first) && root.TryGetProperty("last_name", out var last))
            {
                var combined = $"{first.GetString()} {last.GetString()}".Trim();
                if (!string.IsNullOrWhiteSpace(combined)) return combined;
            }
        }
        catch (JsonException)
        {
            // Falls through to the id below.
        }
        return fallback;
    }

    private static string Prettify(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
