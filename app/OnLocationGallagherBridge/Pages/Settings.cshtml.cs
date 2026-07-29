using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OnLocationGallagherBridge.Data;
using OnLocationGallagherBridge.Models;
using OnLocationGallagherBridge.Services;
using Serilog;

namespace OnLocationGallagherBridge.Pages;

public class SettingsModel : PageModel
{
    private readonly ConfigService _config;
    private readonly IOnLocationConnector _onLocation;
    private readonly IGallagherConnector _gallagher;
    private readonly BridgeDbContext _db;

    [BindProperty, Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public BridgeConfig Config { get; set; } = new();

    public string? Message { get; set; }
    public string ConfigFilePath => _config.ConfigFilePath;

    public SettingsModel(ConfigService config, IOnLocationConnector onLocation, IGallagherConnector gallagher, BridgeDbContext db)
    {
        _config = config;
        _onLocation = onLocation;
        _gallagher = gallagher;
        _db = db;
    }

    public void OnGet()
    {
        Config = _config.GetConfig();
    }

    private void LogModelStateErrors()
    {
        if (ModelState.IsValid) return;
        foreach (var entry in ModelState)
        {
            foreach (var error in entry.Value.Errors)
            {
                Log.Warning("Settings ModelState error for {Key}: {Message}", entry.Key, error.ErrorMessage);
            }
        }
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        LogModelStateErrors();
        await _config.SaveAsync(Config);
        Message = "Settings saved and encrypted.";
        return Page();
    }

    public async Task<IActionResult> OnPostTestOnLocationAsync()
    {
        LogModelStateErrors();
        _config.SetConfig(Config);
        var ok = await _onLocation.TestConnectionAsync();
        if (ok)
        {
            await _config.SaveAsync(Config);
        }
        Message = ok ? "OnLocation connection OK" : $"OnLocation connection failed: {await _onLocation.GetLastErrorAsync()}";
        return Page();
    }

    public async Task<IActionResult> OnPostTestGallagherAsync()
    {
        LogModelStateErrors();
        _config.SetConfig(Config);
        var ok = await _gallagher.TestConnectionAsync();
        if (ok)
        {
            await _config.SaveAsync(Config);
        }
        Message = ok ? "Gallagher connection OK" : $"Gallagher connection failed: {await _gallagher.GetLastErrorAsync()}";
        return Page();
    }

    // Clears everything the bridge has learned about records while leaving the connector credentials and the
    // field mapping alone, so an initial match can be run again from scratch.
    public async Task<IActionResult> OnPostResetSyncDataAsync(CancellationToken ct)
    {
        Config = _config.GetConfig();

        var jobs = await _db.SyncJobs.ExecuteDeleteAsync(ct);
        var mappings = await _db.EntityMappings.ExecuteDeleteAsync(ct);
        var manual = await _db.ManualMatchQueues.ExecuteDeleteAsync(ct);
        var bookmarks = await _db.SyncBookmarks.ExecuteDeleteAsync(ct);
        var audit = await _db.AuditLogs.ExecuteDeleteAsync(ct);

        foreach (var profile in await _db.SyncProfiles.ToListAsync(ct))
        {
            profile.Enabled = false;
            profile.InitialMatchCompleted = false;
            profile.InitialMatchCompletedAt = null;
            profile.LastRun = null;
            profile.NextRun = null;
        }
        await _db.SaveChangesAsync(ct);

        Message = $"Cleared {jobs} job(s), {mappings} mapping(s), {manual} manual match row(s), {bookmarks} bookmark(s) and {audit} audit entr(ies). "
                + "Every profile is now disabled and awaiting a fresh initial match. Field mapping and credentials were kept.";
        Log.Warning("Sync data reset from the Settings page: {Message}", Message);
        return Page();
    }

    // Also discards the field mapping, match rules, induction selection and creation defaults.
    public async Task<IActionResult> OnPostResetProfilesAsync(CancellationToken ct)
    {
        await OnPostResetSyncDataAsync(ct);

        foreach (var profile in await _db.SyncProfiles.ToListAsync(ct))
        {
            profile.FieldMapJson = "[]";
            profile.MatchRulesJson = "[]";
            profile.SelectedInductionIdsJson = "[]";
            profile.DefaultDivisionHref = string.Empty;
            profile.DefaultDivisionName = string.Empty;
            profile.DefaultAccessGroupsJson = "[]";
        }
        await _db.SaveChangesAsync(ct);

        Message = "Cleared all sync data, and reset the field mapping, match rules, induction selection and creation defaults on every profile.";
        Log.Warning("Profile configuration reset from the Settings page");
        return Page();
    }

    public IActionResult OnPostClearCredentials()
    {
        _config.Delete();
        Config = _config.GetConfig();
        Message = "Deleted the encrypted connector settings. Both connections must be configured and tested again.";
        Log.Warning("Connector credentials deleted from the Settings page");
        return Page();
    }
}
