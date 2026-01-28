using InvestIt.Repositories.Interfaces;
using InvestIt.Services.Interfaces;

namespace InvestIt.BackgroundServices;

public class NotificationProcessorHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationProcessorHostedService> _logger;

    public NotificationProcessorHostedService(
        IServiceProvider serviceProvider,
        ILogger<NotificationProcessorHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification Processor Hosted Service is starting");

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var notificationRepository = scope.ServiceProvider
                        .GetRequiredService<INotificationQueueRepository>();
                    var notificationService = scope.ServiceProvider
                        .GetRequiredService<INotificationService>();

                    var pendingNotifications = await notificationRepository.GetPendingAsync(100);

                    if (pendingNotifications.Any())
                    {
                        _logger.LogInformation("Processing {Count} pending notifications", pendingNotifications.Count());

                        foreach (var notification in pendingNotifications)
                        {
                            try
                            {
                                if (notification.Transaction == null)
                                {
                                    _logger.LogWarning(
                                        "Notification {NotificationId} has null Transaction, skipping",
                                        notification.Id);
                                    await notificationRepository.MarkAsFailedAsync(notification.Id, "Transaction was null");
                                    continue;
                                }

                                _logger.LogDebug(
                                    "Processing notification {NotificationId} for transaction {TxHash}",
                                    notification.Id, notification.Transaction.TxHash);

                                await notificationService.SendTransactionNotificationAsync(notification.Transaction);
                                await notificationRepository.MarkAsProcessedAsync(notification.Id);

                                _logger.LogDebug(
                                    "Successfully processed notification {NotificationId}",
                                    notification.Id);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error sending notification {NotificationId}", notification.Id);
                                await notificationRepository.MarkAsFailedAsync(notification.Id, ex.Message);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in notification processing cycle");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Notification Processor Hosted Service is stopping");
    }
}
