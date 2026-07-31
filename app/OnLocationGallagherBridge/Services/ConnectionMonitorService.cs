namespace OnLocationGallagherBridge.Services;

public class ConnectionMonitorService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ConnectionMonitorService> _logger;

    public ConnectionMonitorService(IServiceProvider services, ILogger<ConnectionMonitorService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Connection monitor started");

        // Give the service a moment to settle before the first check.
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var config = scope.ServiceProvider.GetRequiredService<ConfigService>();
                if (!config.GetConfig().Notifications.Enabled)
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    continue;
                }

                var onLocation = scope.ServiceProvider.GetRequiredService<IOnLocationConnector>();
                var gallagher = scope.ServiceProvider.GetRequiredService<IGallagherConnector>();

                await onLocation.TestConnectionAsync(stoppingToken, raiseNotifications: true);
                await gallagher.TestConnectionAsync(stoppingToken, raiseNotifications: true);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection monitor failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
