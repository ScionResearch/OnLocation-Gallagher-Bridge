using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
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

    [BindProperty]
    public IFormFile? CertificateFile { get; set; }

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

        var current = _config.GetConfig();
        var users = current.WebHost.Auth.Users;
        Config.WebHost.Auth.Users = users;

        if (Config.WebHost.Https.CertificateSource == "Pfx" && CertificateFile is { Length: > 0 })
        {
            var certDir = Path.Combine(_config.ConfigDirectory, "certs");
            Directory.CreateDirectory(certDir);
            var uploadPath = Path.Combine(certDir, "uploaded-cert.pfx");
            using var stream = new FileStream(uploadPath, FileMode.Create);
            await CertificateFile.CopyToAsync(stream);
            Config.WebHost.Https.CertificatePath = uploadPath;
        }

        // Push the pending settings into memory so the live connectors use them during the test.
        _config.SetConfig(Config);

        var olOk = await _onLocation.TestConnectionAsync();
        var olError = olOk ? null : await _onLocation.GetLastErrorAsync();

        var gallOk = await _gallagher.TestConnectionAsync();
        var gallError = gallOk ? null : await _gallagher.GetLastErrorAsync();

        // Persist the settings even if a test fails.
        await _config.SaveAsync(Config);

        var parts = new List<string> { "Settings saved." };
        if (olOk && gallOk)
        {
            parts.Add("Both connections verified.");
            ConnectorStatus = ConfigurationStatus.Complete;
        }
        else
        {
            if (!olOk) parts.Add($"OnLocation connection failed: {olError}");
            if (!gallOk) parts.Add($"Gallagher connection failed: {gallError}");
            ConnectorStatus = ConfigurationStatus.Faulty;
        }

        Message = string.Join(" ", parts);
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
