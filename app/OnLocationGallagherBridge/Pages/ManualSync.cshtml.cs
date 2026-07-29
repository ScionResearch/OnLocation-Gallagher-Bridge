using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OnLocationGallagherBridge.Data;
using OnLocationGallagherBridge.Models;
using OnLocationGallagherBridge.Services;
using Serilog;
using System.Text.Json;

namespace OnLocationGallagherBridge.Pages;

public class ManualSyncModel : PageModel
{
    private readonly BridgeDbContext _db;
    private readonly IOnLocationSourceService _source;
    private readonly IJobProcessor _processor;

    [BindProperty]
    public string? SelectedProfileId { get; set; }

    public List<SelectListItem> ProfileOptions { get; set; } = new();
    public List<SyncJob> RecentJobs { get; set; } = new();
    public string? Message { get; set; }

    public ManualSyncModel(BridgeDbContext db, IOnLocationSourceService source, IJobProcessor processor)
    {
        _db = db;
        _source = source;
        _processor = processor;
    }

    public async Task OnGetAsync(CancellationToken ct)
    {
        var profiles = await _db.SyncProfiles.AsNoTracking().ToListAsync(ct);
        ProfileOptions = profiles.Select(p => new SelectListItem(p.Id, p.Id)).ToList();
        var jobs = await _db.SyncJobs.AsNoTracking().ToListAsync(ct);
        RecentJobs = jobs.OrderByDescending(j => j.UpdatedAt).Take(50).ToList();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var profiles = await _db.SyncProfiles.AsNoTracking().ToListAsync(ct);
        ProfileOptions = profiles.Select(p => new SelectListItem(p.Id, p.Id)).ToList();

        var profile = profiles.FirstOrDefault(p => p.Id == SelectedProfileId);
        if (profile == null)
        {
            Message = "Profile not found.";
            return Page();
        }

        var correlation = Guid.NewGuid().ToString("N");
        var bookmark = await _db.SyncBookmarks.FindAsync(new object?[] { profile.Id }, cancellationToken: ct);
        var isNewBookmark = bookmark == null;
        bookmark ??= new SyncBookmark { ProfileId = profile.Id };

        Log.Information("Starting manual sync for profile {Profile} ({EntityType})", profile.Id, profile.EntityType);

        IReadOnlyList<JsonElement> records;
        try
        {
            records = await _source.GetRecordsAsync(profile, bookmark, ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Manual sync fetch failed for profile {Profile}", profile.Id);
            Message = $"Fetch failed: {ex.Message}";
            return Page();
        }

        Log.Information("Fetched {Count} records for profile {Profile}", records.Count, profile.Id);

        int created = 0;
        foreach (var record in records)
        {
            var id = GetId(record);
            if (string.IsNullOrEmpty(id)) continue;
            var job = new SyncJob
            {
                ProfileId = profile.Id,
                SourceType = profile.EntityType,
                SourceId = id,
                PayloadJson = record.ToString() ?? "{}",
                Status = "Pending",
                CorrelationId = correlation
            };
            _db.SyncJobs.Add(job);
            created++;
        }
        await _db.SaveChangesAsync(ct);

        var pending = await _db.SyncJobs.Where(j => j.ProfileId == profile.Id && (j.Status == "Pending" || j.Status == "ManualReview")).ToListAsync(ct);
        foreach (var job in pending)
        {
            job.Status = "Running";
            await _db.SaveChangesAsync(ct);
            await _processor.ProcessAsync(job, correlation, ct);
        }

        if (isNewBookmark)
            _db.SyncBookmarks.Add(bookmark);
        else
            _db.SyncBookmarks.Update(bookmark);
        await _db.SaveChangesAsync(ct);

        Message = records.Count == 0
            ? "No records returned from OnLocation. The endpoint responded but the list was empty."
            : $"Fetched {records.Count} records; created {created} sync jobs.";
        Log.Information("Manual sync completed for profile {Profile}: {Message}", profile.Id, Message);
        var jobs = await _db.SyncJobs.AsNoTracking().ToListAsync(ct);
        RecentJobs = jobs.OrderByDescending(j => j.UpdatedAt).Take(50).ToList();
        return Page();
    }

    private static string? GetId(JsonElement record)
    {
        if (record.TryGetProperty("id", out var id)) return id.ValueKind == JsonValueKind.String ? id.GetString() : id.GetRawText();
        return null;
    }

    public string GetDisplay(SyncJob job)
    {
        try
        {
            using var doc = JsonDocument.Parse(job.PayloadJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
            {
                var name = nameProp.GetString();
                if (!string.IsNullOrWhiteSpace(name)) return $"{name} ({job.SourceId})";
            }
            if (root.TryGetProperty("first_name", out var first) && root.TryGetProperty("last_name", out var last)
                && first.ValueKind == JsonValueKind.String && last.ValueKind == JsonValueKind.String)
            {
                var display = $"{first.GetString()} {last.GetString()}".Trim();
                if (!string.IsNullOrWhiteSpace(display)) return $"{display} ({job.SourceId})";
            }
            if (root.TryGetProperty("email", out var email) && email.ValueKind == JsonValueKind.String)
            {
                var addr = email.GetString();
                if (!string.IsNullOrWhiteSpace(addr)) return $"{addr} ({job.SourceId})";
            }
        }
        catch { }
        return job.SourceId;
    }
}
