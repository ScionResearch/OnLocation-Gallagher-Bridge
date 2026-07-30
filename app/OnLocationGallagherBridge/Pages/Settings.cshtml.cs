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
    private readonly IConfigurationStatusService _statusService;

    [BindProperty, Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public BridgeConfig Config { get; set; } = new();

    public string? Message { get; set; }
    public ConfigurationStatus ConnectorStatus { get; set; }

    public SettingsModel(ConfigService config, IOnLocationConnector onLocation, IGallagherConnector gallagher, IConfigurationStatusService statusService)
    {
        _config = config;
        _onLocation = onLocation;
        _gallagher = gallagher;
        _statusService = statusService;
    }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Config = _config.GetConfig();
        ConnectorStatus = await _statusService.GetConnectorSettingsStatusAsync(Config, testConnections: true, ct);
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
        ConnectorStatus = await _statusService.GetConnectorSettingsStatusAsync(Config, testConnections: false);
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
        ConnectorStatus = await _statusService.GetConnectorSettingsStatusAsync(Config, testConnections: false);
        if (!ok && ConnectorStatus == ConfigurationStatus.Complete)
            ConnectorStatus = ConfigurationStatus.Faulty;
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
        ConnectorStatus = await _statusService.GetConnectorSettingsStatusAsync(Config, testConnections: false);
        if (!ok && ConnectorStatus == ConfigurationStatus.Complete)
            ConnectorStatus = ConfigurationStatus.Faulty;
        return Page();
    }
}
