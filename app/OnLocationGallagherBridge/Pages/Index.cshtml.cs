using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OnLocationGallagherBridge.Data;
using OnLocationGallagherBridge.Models;
using OnLocationGallagherBridge.Services;

namespace OnLocationGallagherBridge.Pages;

public class IndexModel : PageModel
{
    private readonly BridgeDbContext _db;
    private readonly IOnLocationConnector _onLocation;
    private readonly IGallagherConnector _gallagher;
    private readonly IAuditService _audit;

    public IndexModel(BridgeDbContext db, IOnLocationConnector onLocation, IGallagherConnector gallagher, IAuditService audit)
    {
        _db = db;
        _onLocation = onLocation;
        _gallagher = gallagher;
        _audit = audit;
    }

    public List<SyncProfile> Profiles { get; set; } = new();
    public int PendingJobs { get; set; }
    public int FailedJobs { get; set; }
    public int ManualMatches { get; set; }
    public bool OnLocationOk { get; set; }
    public bool GallagherOk { get; set; }
    public IReadOnlyList<AuditLog> RecentAudit { get; set; } = Array.Empty<AuditLog>();
    public string? Message { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Profiles = await _db.SyncProfiles.AsNoTracking().ToListAsync(ct);
        PendingJobs = await _db.SyncJobs.CountAsync(j => j.Status == "Pending", ct);
        FailedJobs = await _db.SyncJobs.CountAsync(j => j.Status == "Failed", ct);
        ManualMatches = await _db.ManualMatchQueues.CountAsync(m => m.Status == "Pending", ct);
        OnLocationOk = await _onLocation.TestConnectionAsync(ct);
        GallagherOk = await _gallagher.TestConnectionAsync(ct);
        RecentAudit = await _audit.GetRecentAsync(20);
        if (TempData["Message"] is string message) Message = message;
    }

    // The sync engine skips profiles that are not enabled, so without this the flag could only be changed by
    // confirming an initial match.
    public async Task<IActionResult> OnPostToggleAsync(string profileId, bool enable, CancellationToken ct)
    {
        var profile = await _db.SyncProfiles.FindAsync(new object?[] { profileId }, cancellationToken: ct);
        if (profile == null)
        {
            TempData["Message"] = $"Profile '{profileId}' was not found.";
            return RedirectToPage();
        }

        if (enable && !profile.InitialMatchCompleted)
        {
            TempData["Message"] = $"Confirm the initial record match for '{profileId}' before enabling sync, otherwise every record is sent to the manual match queue.";
            return RedirectToPage();
        }

        profile.Enabled = enable;
        // Run on the next engine tick rather than waiting out the remainder of the previous interval.
        if (enable) profile.NextRun = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        TempData["Message"] = enable
            ? $"Sync enabled for '{profileId}'. The engine picks it up within a minute."
            : $"Sync disabled for '{profileId}'.";
        return RedirectToPage();
    }
}
