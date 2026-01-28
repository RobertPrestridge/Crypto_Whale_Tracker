using InvestIt.Services.Interfaces;
using InvestIt.Services.WhaleTracking;

namespace InvestIt.BackgroundServices;

public class BlockchainMonitoringHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BlockchainMonitoringHostedService> _logger;
    private readonly IConfiguration _configuration;
    private readonly int _pollingIntervalSeconds;

    public BlockchainMonitoringHostedService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<BlockchainMonitoringHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        _pollingIntervalSeconds = configuration.GetValue<int>("MonitoringSettings:PollingIntervalSeconds", 30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Blockchain Monitoring Hosted Service is starting (polling every {Interval}s)",
            _pollingIntervalSeconds);

        // Wait a bit before starting to allow app initialization
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting blockchain monitoring cycle");

                // Create a scope to resolve scoped services
                using (var scope = _serviceProvider.CreateScope())
                {
                    var monitoringService = scope.ServiceProvider
                        .GetRequiredService<IBlockchainMonitoringService>();

                    await monitoringService.MonitorAllChainsAsync();

                    // Monitor whale transactions if enabled (both strategies for maximum coverage)
                    var whaleTrackingEnabled = _configuration.GetValue<bool>("WhaleTracking:Enabled", true);
                    if (whaleTrackingEnabled)
                    {
                        // Strategy 1: Scan high-traffic DEX contracts and token contracts
                        var globalWhaleMonitor = scope.ServiceProvider
                            .GetRequiredService<GlobalWhaleMonitorService>();

                        await globalWhaleMonitor.MonitorRecentBlocksAsync();

                        // Strategy 2: Monitor specific exchange whale wallets
                        var whaleWalletMonitor = scope.ServiceProvider
                            .GetRequiredService<WhaleTransactionMonitorService>();

                        await whaleWalletMonitor.MonitorWhaleTransactionsAsync();
                    }
                }

                _logger.LogInformation(
                    "Blockchain monitoring cycle completed. Next poll in {Interval}s",
                    _pollingIntervalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during blockchain monitoring cycle");
            }

            // Wait for the polling interval or until cancellation
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_pollingIntervalSeconds),
                    stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected when service is stopping
                break;
            }
        }

        _logger.LogInformation("Blockchain Monitoring Hosted Service is stopping");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Blockchain Monitoring Hosted Service stop requested");
        await base.StopAsync(cancellationToken);
    }
}
