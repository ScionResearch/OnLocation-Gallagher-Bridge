using Microsoft.AspNetCore.Mvc;
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
    private readonly Serilog.ILogger _logger;

    public ExceptionsModel(BridgeDbContext db)
    {
        _db = db;
        _logger = Serilog.Log.Logger.ForContext<ExceptionsModel>();
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

    public List<ErrorGroup> Groups { get; set; } = new();
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
