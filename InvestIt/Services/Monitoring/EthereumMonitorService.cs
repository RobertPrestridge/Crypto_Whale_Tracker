using InvestIt.Repositories.Interfaces;
using InvestIt.Services.ExplorerClients;
using InvestIt.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace InvestIt.Services.Monitoring;

public class EthereumMonitorService : IChainMonitorService
{
    private readonly EtherscanClient _apiClient;
    private readonly IWalletRepository _walletRepository;
    private readonly ITransactionProcessingService _transactionProcessor;
    private readonly IMonitoringStatusRepository _monitoringStatusRepository;
    private readonly IRateLimitService _rateLimitService;
    private readonly ILogger<EthereumMonitorService> _logger;
    private readonly int _maxTransactionsPerPoll;

    public string NetworkName => "Ethereum";
    public int NetworkId => 1; // Ethereum network ID from seed data

    public EthereumMonitorService(
        EtherscanClient apiClient,
        IWalletRepository walletRepository,
        ITransactionProcessingService transactionProcessor,
        IMonitoringStatusRepository monitoringStatusRepository,
        IRateLimitService rateLimitService,
        IConfiguration configuration,
        ILogger<EthereumMonitorService> logger)
    {
        _apiClient = apiClient;
        _walletRepository = walletRepository;
        _transactionProcessor = transactionProcessor;
        _monitoringStatusRepository = monitoringStatusRepository;
        _rateLimitService = rateLimitService;
        _logger = logger;
        _maxTransactionsPerPoll = configuration.GetValue<int>("MonitoringSettings:MaxTransactionsPerPoll", 100);
    }

    public async Task MonitorWalletsAsync()
    {
        try
        {
            _logger.LogInformation("Starting Ethereum wallet monitoring");

            var wallets = await _walletRepository.GetByNetworkAsync(NetworkId);
            var activeWallets = wallets.Where(w => w.IsActive).ToList();

            if (!activeWallets.Any())
            {
                _logger.LogDebug("No active wallets to monitor for Ethereum");
                await UpdateMonitoringStatusAsync(true);
                return;
            }

            _logger.LogInformation("Monitoring {Count} Ethereum wallets", activeWallets.Count);

            foreach (var wallet in activeWallets)
            {
                await MonitorSingleWalletAsync(wallet.Id, wallet.Address);
            }

            await UpdateMonitoringStatusAsync(true);
            _logger.LogInformation("Completed Ethereum wallet monitoring");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Ethereum wallet monitoring");
            await UpdateMonitoringStatusAsync(false, ex.Message);
        }
    }

    private async Task MonitorSingleWalletAsync(int walletId, string address)
    {
        try
        {
            // Check rate limit
            if (!await _rateLimitService.TryAcquireTokenAsync(NetworkId))
            {
                _logger.LogWarning("Rate limit exceeded for Ethereum, skipping wallet {Address}", address);
                return;
            }

            // Fetch transactions
            var transactions = await _apiClient.GetTransactionsAsync(address, _maxTransactionsPerPoll);

            var processedCount = 0;
            foreach (var tx in transactions)
            {
                var result = await _transactionProcessor.ProcessAndStoreAsync(tx, walletId, address);
                if (result != null)
                {
                    processedCount++;
                }
            }

            if (processedCount > 0)
            {
                _logger.LogInformation(
                    "Processed {Count} new transactions for Ethereum wallet {Address}",
                    processedCount, address);
            }

            // Update wallet last checked time
            var wallet = await _walletRepository.GetByIdAsync(walletId);
            if (wallet != null)
            {
                wallet.LastCheckedAt = DateTime.UtcNow;
                await _walletRepository.UpdateAsync(wallet);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring Ethereum wallet {Address}", address);
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
            _logger.LogError(ex, "Error updating monitoring status for Ethereum");
        }
    }
}
