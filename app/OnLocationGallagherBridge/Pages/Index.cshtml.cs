using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OnLocationGallagherBridge.Data;
using OnLocationGallagherBridge.Models;
using OnLocationGallagherBridge.Services;
using Serilog;

namespace OnLocationGallagherBridge.Pages;

public class IndexModel : PageModel
{
    private readonly BridgeDbContext _db;
    private readonly IOnLocationConnector _onLocation;
    private readonly IGallagherConnector _gallagher;
    private readonly IAuditService _audit;
    private readonly ISyncActivity _activity;
    private readonly IConfigurationStatusService _statusService;
    private readonly IOnLocationSourceService _source;
    private readonly IJobProcessor _processor;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(BridgeDbContext db, IOnLocationConnector onLocation, IGallagherConnector gallagher, IAuditService audit, ISyncActivity activity, IConfigurationStatusService statusService, IOnLocationSourceService source, IJobProcessor processor, ILogger<IndexModel> logger)
    {
        _db = db;
        _onLocation = onLocation;
        _gallagher = gallagher;
        _audit = audit;
        _activity = activity;
        _statusService = statusService;
        _source = source;
        _processor = processor;
        _logger = logger;
    }

    public List<SyncProfile> Profiles { get; set; } = new();
    public int PendingJobs { get; set; }
    public int FailedJobs { get; set; }
    public int ManualMatches { get; set; }
    public bool OnLocationOk { get; set; }
    public bool GallagherOk { get; set; }
    public IReadOnlyList<AuditLog> RecentAudit { get; set; } = Array.Empty<AuditLog>();
    public string? Message { get; set; }
    public DateTimeOffset? NextRun { get; set; }
    public string? NextRunProfileId { get; set; }
    public DateTimeOffset? NextFullRun { get; set; }
    public string? NextFullRunProfileId { get; set; }
    public SyncActivitySnapshot Activity { get; set; } = new();
    public ConfigurationState OverallStatus { get; set; } = new(ConfigurationStatus.NotConfigured, ConfigurationStatus.NotConfigured, ConfigurationStatus.NotConfigured, ConfigurationStatus.NotConfigured);
    public Dictionary<string, ConfigurationStatus> ProfileMappingStatus { get; set; } = new();
    public Dictionary<string, ConfigurationStatus> ProfileInitialMatchStatus { get; set; } = new();

    // Fast sync can run as often as once a minute but no more than hourly.
    public static readonly (int Minutes, string Label)[] IntervalOptions =
    {
        (1, "Every minute"),
        (2, "Every 2 minutes"),
        (3, "Every 3 minutes"),
        (5, "Every 5 minutes"),
        (10, "Every 10 minutes"),
        (15, "Every 15 minutes"),
        (20, "Every 20 minutes"),
        (30, "Every 30 minutes"),
        (45, "Every 45 minutes"),
        (60, "Hourly")
    };

    // Full sync runs from daily up to roughly monthly.
    public static readonly (int Days, string Label)[] FullIntervalOptions =
    {
        (1, "Daily"),
        (2, "Every 2 days"),
        (3, "Every 3 days"),
        (7, "Weekly"),
        (14, "Fortnightly"),
        (30, "Monthly")
    };

    public static string DescribeInterval(int minutes)
    {
        var match = IntervalOptions.FirstOrDefault(o => o.Minutes == minutes);
        return match.Label ?? $"Every {minutes} minute(s)";
    }

    public static string DescribeFullInterval(int days)
    {
        var match = FullIntervalOptions.FirstOrDefault(o => o.Days == days);
        return match.Label ?? $"Every {days} day(s)";
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
        var nextProfile = Profiles.Where(p => p.Enabled).OrderBy(p => p.NextRun ?? DateTimeOffset.MaxValue).FirstOrDefault();
        NextRunProfileId = nextProfile?.Id;
        NextRun = nextProfile?.NextRun;
        var nextFullProfile = Profiles.Where(p => p.Enabled).OrderBy(p => p.NextFullRun ?? DateTimeOffset.MaxValue).FirstOrDefault();
        NextFullRunProfileId = nextFullProfile?.Id;
        NextFullRun = nextFullProfile?.NextFullRun;
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

    public async Task<IActionResult> OnPostScheduleAsync(string profileId, int fastMinutes, int fastRecordCount, int fullDays, string fullTime, int fullMonths, CancellationToken ct)
    {
        var profile = await _db.SyncProfiles.FindAsync(new object?[] { profileId }, cancellationToken: ct);
        if (profile == null)
        {
            TempData["Message"] = $"Profile '{profileId}' was not found.";
            return RedirectToPage();
        }

        if (fastMinutes < 1 || fastMinutes > 60)
        {
            TempData["Message"] = "The fast sync interval must be between 1 and 60 minutes.";
            return RedirectToPage();
        }

        if (fastRecordCount < 1 || fastRecordCount > 200)
        {
            TempData["Message"] = "The fast sync record count must be between 1 and 200.";
            return RedirectToPage();
        }

        if (fullDays < 1 || fullDays > 31)
        {
            TempData["Message"] = "The full sync interval must be between 1 and 31 days.";
            return RedirectToPage();
        }

        if (!TimeSpan.TryParseExact(fullTime, ["hh\\:mm", "h\\:mm"], CultureInfo.InvariantCulture, out var fullTimeOfDay) || fullTimeOfDay.TotalMinutes >= 1440)
        {
            TempData["Message"] = "The full sync time of day must be a valid time (e.g. 01:00).";
            return RedirectToPage();
        }

        if (fullMonths < 0 || fullMonths > 120)
        {
            TempData["Message"] = "The full sync lookback must be between 1 and 120 months, or 0 for all existing records.";
            return RedirectToPage();
        }

        profile.FastSyncIntervalMinutes = fastMinutes;
        profile.FastSyncRecordCount = fastRecordCount;
        profile.FullSyncIntervalDays = fullDays;
        profile.FullSyncTimeOfDayMinutes = (int)fullTimeOfDay.TotalMinutes;
        profile.FullSyncLookbackMonths = fullMonths == 0 ? null : fullMonths;
        // Make the new schedule take effect from now rather than waiting for the previous schedule.
        profile.NextRun = DateTimeOffset.UtcNow.AddMinutes(fastMinutes);
        profile.NextFullRun = SyncEngine.ComputeNextFullRun(DateTimeOffset.UtcNow, profile);
        await _db.SaveChangesAsync(ct);

        var lookbackText = fullMonths == 0 ? "all existing records" : $"{fullMonths} month(s)";
        TempData["Message"] = $"'{profileId}' now fast-syncs {DescribeInterval(fastMinutes).ToLowerInvariant()} retrieving {fastRecordCount} record(s) and full-syncs {DescribeFullInterval(fullDays).ToLowerInvariant()} at {fullTime} looking back {lookbackText}.";
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

    public async Task<IActionResult> OnPostRunFastAsync(string profileId, CancellationToken ct)
        => await RunSyncAsync(profileId, fullSync: false, ct);

    public async Task<IActionResult> OnPostRunFullAsync(string profileId, CancellationToken ct)
        => await RunSyncAsync(profileId, fullSync: true, ct);

    private async Task<IActionResult> RunSyncAsync(string profileId, bool fullSync, CancellationToken ct)
    {
        var status = await _statusService.GetOverallStateAsync(false, ct);
        if (status.Overall != ConfigurationStatus.Complete)
        {
            TempData["Message"] = "Complete setup before running a manual sync.";
            return RedirectToPage();
        }

        var profile = await _db.SyncProfiles.FindAsync(new object?[] { profileId }, cancellationToken: ct);
        if (profile == null)
        {
            TempData["Message"] = $"Profile '{profileId}' was not found.";
            return RedirectToPage();
        }

        if (!profile.Enabled)
        {
            TempData["Message"] = $"Enable '{profileId}' before running a manual sync.";
            return RedirectToPage();
        }

        Log.Information("Starting manual {SyncType} sync for profile {Profile} ({EntityType})", fullSync ? "full" : "fast", profile.Id, profile.EntityType);
        var summary = await SyncProfileRunner.RunAsync(_db, _source, _processor, _audit, _activity, _logger, profile, fullSync, ct);
        Log.Information("Manual {SyncType} sync completed for profile {Profile}: {Message}", fullSync ? "full" : "fast", profile.Id, summary.Message);
        TempData["Message"] = summary.Message;
        return RedirectToPage();
    }

    private static string? GetId(JsonElement record)
    {
        if (record.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            return id.GetString();
        if (record.TryGetProperty("id", out var idNum) && idNum.ValueKind == JsonValueKind.Number)
            return idNum.ToString();
        return null;
    }
}
