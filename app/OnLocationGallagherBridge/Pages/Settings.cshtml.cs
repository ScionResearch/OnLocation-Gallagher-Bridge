using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OnLocationGallagherBridge.Models;
using OnLocationGallagherBridge.Services;
using Serilog;

namespace OnLocationGallagherBridge.Pages;

public class SettingsModel : PageModel
{
    private readonly ConfigService _config;
    private readonly IOnLocationConnector _onLocation;
    private readonly IGallagherConnector _gallagher;

    [BindProperty, Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public BridgeConfig Config { get; set; } = new();

    public string? Message { get; set; }

    public SettingsModel(ConfigService config, IOnLocationConnector onLocation, IGallagherConnector gallagher)
    {
        _config = config;
        _onLocation = onLocation;
        _gallagher = gallagher;
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
        Config.OnLocationConnectionTestedAt = null;
        Config.GallagherConnectionTestedAt = null;
        await _config.SaveAsync(Config);
        Message = "Settings saved and encrypted. Connection tests must be run again.";
        return Page();
    }

    public async Task<IActionResult> OnPostTestOnLocationAsync()
    {
        LogModelStateErrors();
        var previousGallagherTest = _config.GetConfig().GallagherConnectionTestedAt;
        _config.SetConfig(Config);
        var ok = await _onLocation.TestConnectionAsync();
        if (ok)
        {
            Config.OnLocationConnectionTestedAt = DateTimeOffset.UtcNow;
            Config.GallagherConnectionTestedAt = previousGallagherTest;
            await _config.SaveAsync(Config);
        }
        Message = ok ? "OnLocation connection OK" : $"OnLocation connection failed: {await _onLocation.GetLastErrorAsync()}";
        return Page();
    }

    public async Task<IActionResult> OnPostTestGallagherAsync()
    {
        LogModelStateErrors();
        var previousOnLocationTest = _config.GetConfig().OnLocationConnectionTestedAt;
        _config.SetConfig(Config);
        var ok = await _gallagher.TestConnectionAsync();
        if (ok)
        {
            Config.OnLocationConnectionTestedAt = previousOnLocationTest;
            Config.GallagherConnectionTestedAt = DateTimeOffset.UtcNow;
            await _config.SaveAsync(Config);
        }
        Message = ok ? "Gallagher connection OK" : $"Gallagher connection failed: {await _gallagher.GetLastErrorAsync()}";
        return Page();
    }
}
