using System.Text.Json;

namespace OnLocationGallagherBridge.Services;

public record ServiceStatus(
    string Status,
    string Url,
    string OverallStatus,
    bool? OnLocationConnected,
    bool? GallagherConnected,
    DateTimeOffset Timestamp);

public class StatusFileService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<StatusFileService> _logger;
    private readonly string _statusPath;

    public StatusFileService(IServiceProvider services, IHostApplicationLifetime lifetime, ILogger<StatusFileService> logger)
    {
        _services = services;
        _lifetime = lifetime;
        _logger = logger;
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        _statusPath = Path.Combine(programData, "OnLocation-Gallagher-Bridge", "service-status.json");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait until the host has finished starting so that URLs and configuration are available.
        while (!_lifetime.ApplicationStarted.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
            await Task.Delay(200, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await WriteStatusAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutdown.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write status file");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        try { await WriteStatusAsync(stopped: true); } catch { /* best effort */ }
    }

    private async Task WriteStatusAsync(CancellationToken ct = default, bool stopped = false)
    {
        using var scope = _services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<ConfigService>();
        var cfg = config.GetConfig();

        var scheme = cfg.WebHost.Https.Enabled ? "https" : "http";
        var firstUrl = $"{scheme}://*:{cfg.WebHost.Port}";

        var overall = ConfigurationStatus.NotConfigured.ToString();
        try
        {
            var statusService = scope.ServiceProvider.GetRequiredService<IConfigurationStatusService>();
            var state = await statusService.GetOverallStateAsync(false);
            overall = state.Overall.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read configuration status");
        }

        bool? onLocation = null;
        bool? gallagher = null;
        try
        {
            var ol = scope.ServiceProvider.GetRequiredService<IOnLocationConnector>();
            onLocation = ol.LastConnectionResult;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read OnLocation connection state");
        }

        try
        {
            var gc = scope.ServiceProvider.GetRequiredService<IGallagherConnector>();
            gallagher = gc.LastConnectionResult;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read Gallagher connection state");
        }

        var status = new ServiceStatus(
            Status: stopped ? "Stopped" : "Running",
            Url: firstUrl,
            OverallStatus: overall,
            OnLocationConnected: onLocation,
            GallagherConnected: gallagher,
            Timestamp: DateTimeOffset.UtcNow);

        var dir = Path.GetDirectoryName(_statusPath)!;
        Directory.CreateDirectory(dir);

        var options = new JsonSerializerOptions { WriteIndented = true };
        var tmp = _statusPath + ".tmp";
        await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(status, options), ct);
        File.Move(tmp, _statusPath, true);
    }
}
