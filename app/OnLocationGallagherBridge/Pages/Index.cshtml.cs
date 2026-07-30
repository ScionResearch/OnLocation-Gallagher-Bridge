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
    private readonly ISyncActivity _activity;
    private readonly IConfigurationStatusService _statusService;

    public IndexModel(BridgeDbContext db, IOnLocationConnector onLocation, IGallagherConnector gallagher, IAuditService audit, ISyncActivity activity, IConfigurationStatusService statusService)
    {
        _db = db;
        _onLocation = onLocation;
        _gallagher = gallagher;
        _audit = audit;
        _activity = activity;
        _statusService = statusService;
    }

    public List<SyncProfile> Profiles { get; set; } = new();
    public int PendingJobs { get; set; }
    public int FailedJobs { get; set; }
    public int ManualMatches { get; set; }
    public bool OnLocationOk { get; set; }
    public bool GallagherOk { get; set; }
    public IReadOnlyList<AuditLog> RecentAudit { get; set; } = Array.Empty<AuditLog>();
    public string? Message { get; set; }
    public SyncActivitySnapshot Activity { get; set; } = new();
    public ConfigurationState OverallStatus { get; set; } = new(ConfigurationStatus.NotConfigured, ConfigurationStatus.NotConfigured, ConfigurationStatus.NotConfigured, ConfigurationStatus.NotConfigured);
    public Dictionary<string, ConfigurationStatus> ProfileMappingStatus { get; set; } = new();
    public Dictionary<string, ConfigurationStatus> ProfileInitialMatchStatus { get; set; } = new();

    // Anything from once a minute to once a month, which is the range operators asked for. Stored as minutes on
    // the profile so the sync engine needs no changes.
    public static readonly (int Minutes, string Label)[] IntervalOptions =
    {
        (1, "Every minute"),
        (5, "Every 5 minutes"),
        (15, "Every 15 minutes"),
        (30, "Every 30 minutes"),
        (60, "Hourly"),
        (240, "Every 4 hours"),
        (720, "Every 12 hours"),
        (1440, "Daily"),
        (10080, "Weekly"),
        (43200, "Monthly")
    };

    public static string DescribeInterval(int minutes)
    {
        var match = IntervalOptions.FirstOrDefault(o => o.Minutes == minutes);
        return match.Label ?? $"Every {minutes} minute(s)";
    }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Profiles = await _db.SyncProfiles.AsNoTracking().ToListAsync(ct);
        foreach (var p in Profiles)
        {
            ProfileMappingStatus[p.Id] = _statusService.GetFieldMappingStatus(p);
            ProfileInitialMatchStatus[p.Id] = _statusService.GetInitialMatchStatus(p);
        }
        PendingJobs = await _db.SyncJobs.CountAsync(j => j.Status == "Pending", ct);
        FailedJobs = await _db.SyncJobs.CountAsync(j => j.Status == "Failed", ct);
        ManualMatches = await _db.ManualMatchQueues.CountAsync(m => m.Status == "Pending", ct);
        OnLocationOk = await _onLocation.TestConnectionAsync(ct);
        GallagherOk = await _gallagher.TestConnectionAsync(ct);
        OverallStatus = await _statusService.GetOverallStateAsync(testConnections: false, ct);
        if (OverallStatus.ConnectorSettings == ConfigurationStatus.Complete && (!OnLocationOk || !GallagherOk))
        {
            var overall = new[] { ConfigurationStatus.Faulty, OverallStatus.FieldMapping, OverallStatus.InitialMatch }.Min();
            OverallStatus = OverallStatus with { ConnectorSettings = ConfigurationStatus.Faulty, Overall = overall };
        }
        RecentAudit = await _audit.GetRecentAsync(20);
        Activity = _activity.Snapshot;
        if (TempData["Message"] is string message) Message = message;
    }

    // Polled by the dashboard so the activity card updates without reloading the page.
    public IActionResult OnGetActivity()
    {
        var snapshot = _activity.Snapshot;
        return new JsonResult(new
        {
            running = snapshot.Running,
            profileId = snapshot.ProfileId,
            phase = snapshot.Phase,
            detail = snapshot.Detail,
            recordsChecked = snapshot.RecordsChecked,
            recordsMatched = snapshot.RecordsMatched,
            processed = snapshot.Processed,
            total = snapshot.Total,
            elapsed = snapshot.Elapsed,
            lastRunSummary = snapshot.LastRunSummary,
            lastRunFinishedAt = snapshot.LastRunFinishedAt?.ToLocalTime().ToString("g")
        });
    }

    public async Task<IActionResult> OnPostScheduleAsync(string profileId, int minutes, int days, CancellationToken ct)
    {
        var profile = await _db.SyncProfiles.FindAsync(new object?[] { profileId }, cancellationToken: ct);
        if (profile == null)
        {
            TempData["Message"] = $"Profile '{profileId}' was not found.";
            return RedirectToPage();
        }

        if (minutes < 1 || minutes > 43200)
        {
            TempData["Message"] = "The sync interval must be between one minute and one month.";
            return RedirectToPage();
        }

        if (days < 1 || days > 3650)
        {
            TempData["Message"] = "The sync window must be between 1 and 3650 days.";
            return RedirectToPage();
        }

        profile.PollingIntervalMinutes = minutes;
        profile.SyncWindowDays = days;
        // A shorter interval should take effect now rather than after the old one expires.
        if (profile.LastRun.HasValue) profile.NextRun = profile.LastRun.Value.AddMinutes(minutes);
        await _db.SaveChangesAsync(ct);

        TempData["Message"] = $"'{profileId}' now syncs {DescribeInterval(minutes).ToLowerInvariant()}, looking back {days} day(s).";
        return RedirectToPage();
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
