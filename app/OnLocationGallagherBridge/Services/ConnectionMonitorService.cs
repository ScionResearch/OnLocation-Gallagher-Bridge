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
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var config = scope.ServiceProvider.GetRequiredService<ConfigService>();
                var onLocation = scope.ServiceProvider.GetRequiredService<IOnLocationConnector>();
                var gallagher = scope.ServiceProvider.GetRequiredService<IGallagherConnector>();

                var raiseNotifications = config.GetConfig().Notifications.Enabled;

                try
                {
                    await onLocation.TestConnectionAsync(stoppingToken, raiseNotifications);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "OnLocation connection monitor check failed");
                }

                try
                {
                    await gallagher.TestConnectionAsync(stoppingToken, raiseNotifications);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Gallagher connection monitor check failed");
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection monitor failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
