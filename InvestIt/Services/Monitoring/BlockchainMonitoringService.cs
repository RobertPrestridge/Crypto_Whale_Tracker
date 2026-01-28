using InvestIt.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace InvestIt.Services.Monitoring;

public class BlockchainMonitoringService : IBlockchainMonitoringService
{
    private readonly IEnumerable<IChainMonitorService> _chainMonitors;
    private readonly ILogger<BlockchainMonitoringService> _logger;

    public BlockchainMonitoringService(
        IEnumerable<IChainMonitorService> chainMonitors,
        ILogger<BlockchainMonitoringService> logger)
    {
        _chainMonitors = chainMonitors;
        _logger = logger;
    }

    public async Task MonitorAllChainsAsync()
    {
        _logger.LogInformation("Starting monitoring across all blockchain networks");

        // Monitor all chains sequentially to avoid DbContext concurrency issues
        foreach (var monitor in _chainMonitors)
        {
            await MonitorChainWithErrorHandlingAsync(monitor);
        }

        _logger.LogInformation("Completed monitoring across all blockchain networks");
    }

    private async Task MonitorChainWithErrorHandlingAsync(IChainMonitorService monitor)
    {
        try
        {
            _logger.LogDebug("Starting monitoring for {NetworkName}", monitor.NetworkName);
            await monitor.MonitorWalletsAsync();
            _logger.LogDebug("Completed monitoring for {NetworkName}", monitor.NetworkName);
        }
        catch (Exception ex)
        {
            // Error isolation - one chain failure doesn't affect others
            _logger.LogError(
                ex,
                "Failed to monitor {NetworkName}. Other chains continue monitoring.",
                monitor.NetworkName);
        }
    }
}
