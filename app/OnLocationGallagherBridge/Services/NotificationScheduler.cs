namespace OnLocationGallagherBridge.Services;

public class NotificationScheduler : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<NotificationScheduler> _logger;

    public NotificationScheduler(IServiceProvider services, ILogger<NotificationScheduler> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification scheduler started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await notifications.FlushAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification scheduler failed");
            }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
