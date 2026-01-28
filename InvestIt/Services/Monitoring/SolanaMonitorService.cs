using InvestIt.Repositories.Interfaces;
using InvestIt.Services.ExplorerClients;
using InvestIt.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace InvestIt.Services.Monitoring;

public class SolanaMonitorService : IChainMonitorService
{
    private readonly SolanaClient _apiClient;
    private readonly IWalletRepository _walletRepository;
    private readonly IMonitoringStatusRepository _monitoringStatusRepository;
    private readonly ILogger<SolanaMonitorService> _logger;

    public string NetworkName => "Solana";
    public int NetworkId => 4; // Solana network ID from seed data

    public SolanaMonitorService(
        SolanaClient apiClient,
        IWalletRepository walletRepository,
        IMonitoringStatusRepository monitoringStatusRepository,
        ILogger<SolanaMonitorService> logger)
    {
        _apiClient = apiClient;
        _walletRepository = walletRepository;
        _monitoringStatusRepository = monitoringStatusRepository;
        _logger = logger;
    }

    public async Task MonitorWalletsAsync()
    {
        try
        {
            _logger.LogInformation("Starting Solana wallet monitoring (placeholder)");

            var wallets = await _walletRepository.GetByNetworkAsync(NetworkId);
            var activeWallets = wallets.Where(w => w.IsActive).ToList();

            if (!activeWallets.Any())
            {
                _logger.LogDebug("No active wallets to monitor for Solana");
                await UpdateMonitoringStatusAsync(true);
                return;
            }

            _logger.LogInformation("Monitoring {Count} Solana wallets (placeholder implementation)", activeWallets.Count);

            // Placeholder - in production, implement actual Solana monitoring
            await UpdateMonitoringStatusAsync(true);
            _logger.LogInformation("Completed Solana wallet monitoring");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Solana wallet monitoring");
            await UpdateMonitoringStatusAsync(false, ex.Message);
        }
    }

    private async Task UpdateMonitoringStatusAsync(bool isHealthy, string? errorMessage = null)
    {
        try
        {
            var status = await _monitoringStatusRepository.GetByNetworkIdAsync(NetworkId);

            if (status == null)
            {
                status = new Data.Entities.MonitoringStatus
                {
                    NetworkId = NetworkId,
                    LastPollTime = DateTime.UtcNow,
                    IsHealthy = isHealthy,
                    ErrorCount = isHealthy ? 0 : 1,
                    LastErrorMessage = errorMessage,
                    LastErrorTime = isHealthy ? null : DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
            }
            else
            {
                status.LastPollTime = DateTime.UtcNow;
                status.IsHealthy = isHealthy;
                status.ErrorCount = isHealthy ? 0 : status.ErrorCount + 1;
                status.LastErrorMessage = errorMessage;
                status.LastErrorTime = isHealthy ? status.LastErrorTime : DateTime.UtcNow;
                status.UpdatedAt = DateTime.UtcNow;
            }

            await _monitoringStatusRepository.UpsertAsync(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating monitoring status for Solana");
        }
    }
}
