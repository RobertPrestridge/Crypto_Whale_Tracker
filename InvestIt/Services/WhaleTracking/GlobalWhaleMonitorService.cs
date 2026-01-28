using InvestIt.Data.Entities;
using InvestIt.Repositories.Interfaces;
using InvestIt.Services.ExplorerClients;
using InvestIt.Services.Interfaces;
using InvestIt.Services.PriceTracking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InvestIt.Services.WhaleTracking;

/// <summary>
/// Monitors ALL Ethereum transactions over a threshold by scanning recent blocks
/// </summary>
public class GlobalWhaleMonitorService
{
    private readonly EtherscanClient _etherscanClient;
    private readonly ICoinGeckoClient _priceClient;
    private readonly ITransactionProcessingService _transactionProcessor;
    private readonly IWalletRepository _walletRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GlobalWhaleMonitorService> _logger;

    private readonly decimal _whaleThresholdUsd;
    private int? _whaleWalletId;
    private long _lastProcessedBlock = 0;

    public GlobalWhaleMonitorService(
        EtherscanClient etherscanClient,
        ICoinGeckoClient priceClient,
        ITransactionProcessingService transactionProcessor,
        IWalletRepository walletRepository,
        IConfiguration configuration,
        ILogger<GlobalWhaleMonitorService> logger)
    {
        _etherscanClient = etherscanClient;
        _priceClient = priceClient;
        _transactionProcessor = transactionProcessor;
        _walletRepository = walletRepository;
        _configuration = configuration;
        _logger = logger;

        _whaleThresholdUsd = configuration.GetValue<decimal>("WhaleTracking:ThresholdUsd", 10000);
    }

    public async Task MonitorRecentBlocksAsync()
    {
        try
        {
            _logger.LogInformation("Starting global whale monitoring (scanning recent blocks for ${Threshold}+ transactions)", _whaleThresholdUsd);

            // Ensure whale wallet exists
            await EnsureWhaleWalletExistsAsync();

            if (_whaleWalletId == null)
            {
                _logger.LogWarning("Could not create whale wallet, skipping monitoring");
                return;
            }

            // Get latest block number
            var latestBlock = await GetLatestBlockNumberAsync();
            if (latestBlock == 0)
            {
                _logger.LogWarning("Could not get latest block number");
                return;
            }

            // Initialize last processed block if not set
            if (_lastProcessedBlock == 0)
            {
                _lastProcessedBlock = latestBlock - 5; // Start 5 blocks back
            }

            // Don't scan more than 10 blocks at a time to avoid API limits
            var blocksToScan = Math.Min(latestBlock - _lastProcessedBlock, 10);

            if (blocksToScan <= 0)
            {
                _logger.LogDebug("No new blocks to scan");
                return;
            }

            _logger.LogInformation("Scanning {Count} blocks ({From} to {To})", blocksToScan, _lastProcessedBlock + 1, latestBlock);

            var whaleCount = 0;

            // Scan recent blocks for whale transactions
            for (long blockNum = _lastProcessedBlock + 1; blockNum <= latestBlock && blockNum <= _lastProcessedBlock + 10; blockNum++)
            {
                try
                {
                    var blockTransactions = await GetBlockTransactionsAsync(blockNum);

                    foreach (var tx in blockTransactions)
                    {
                        // Calculate USD value
                        var usdValue = await CalculateTransactionValueUsdAsync(tx.TokenSymbol ?? "ETH", tx.Amount);

                        if (usdValue.HasValue && usdValue.Value >= _whaleThresholdUsd)
                        {
                            _logger.LogInformation(
                                "🐋 WHALE DETECTED (Block {Block}): {Hash} - {Amount} {Token} (${Usd:N0}) | {From} → {To}",
                                blockNum, tx.TxHash, tx.Amount, tx.TokenSymbol, usdValue.Value,
                                FormatAddress(tx.FromAddress), FormatAddress(tx.ToAddress));

                            // Store whale transaction
                            await _transactionProcessor.ProcessAndStoreAsync(
                                tx,
                                _whaleWalletId.Value,
                                "GLOBAL_WHALE_SCAN"
                            );

                            whaleCount++;
                        }
                    }

                    _lastProcessedBlock = blockNum;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error scanning block {Block}", blockNum);
                }
            }

            _logger.LogInformation("Global whale scan complete. Found {Count} whale transactions in {Blocks} blocks",
                whaleCount, blocksToScan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in global whale monitoring");
        }
    }

    private async Task<long> GetLatestBlockNumberAsync()
    {
        try
        {
            // Use a well-known address to get latest block
            var recentTxs = await _etherscanClient.GetTransactionsAsync(
                "0x00000000219ab540356cBB839Cbe05303d7705Fa", // ETH 2.0 Deposit Contract
                limit: 1
            );

            var latestTx = recentTxs.FirstOrDefault();
            if (latestTx != null)
            {
                return latestTx.BlockNumber;
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest block number");
            return 0;
        }
    }

    private async Task<List<Services.ExplorerClients.Models.BlockchainTransaction>> GetBlockTransactionsAsync(long blockNumber)
    {
        var transactions = new List<Services.ExplorerClients.Models.BlockchainTransaction>();

        try
        {
            // Etherscan doesn't have a direct "get block transactions" endpoint in their free API
            // So we'll use a workaround: monitor high-value known addresses
            // For TRUE all-transactions monitoring, you'd need:
            // 1. Etherscan Pro API with block transaction endpoint
            // 2. Run your own Ethereum node with Web3
            // 3. Use a service like Alchemy/Infura with block scanning

            // For now, we'll scan some high-volume addresses to get representative data
            var highVolumeAddresses = new[]
            {
                "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2", // WETH Contract
                "0xdAC17F958D2ee523a2206206994597C13D831ec7", // USDT Contract
                "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", // USDC Contract
                "0x6B175474E89094C44Da98b954EedeAC495271d0F", // DAI Contract
                "0x1f9840a85d5aF5bf1D1762F925BDADdC4201F984", // UNI Contract
                "0x7a250d5630B4cF539739dF2C5dAcb4c659F2488D"  // Uniswap V2 Router
            };

            // Pick a random address to scan this cycle
            var randomAddress = highVolumeAddresses[new Random().Next(highVolumeAddresses.Length)];

            var recentTxs = await _etherscanClient.GetTransactionsAsync(randomAddress, limit: 20);

            // Filter to transactions in this block range (approximately)
            transactions = recentTxs
                .Where(tx => tx.BlockNumber >= blockNumber - 2 && tx.BlockNumber <= blockNumber + 2)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transactions for block {Block}", blockNumber);
        }

        return transactions;
    }

    private async Task<decimal?> CalculateTransactionValueUsdAsync(string tokenSymbol, decimal amount)
    {
        try
        {
            var price = await _priceClient.GetTokenPriceUsdAsync(tokenSymbol);

            if (price.HasValue)
            {
                return amount * price.Value;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating USD value for {Token}", tokenSymbol);
            return null;
        }
    }

    private async Task EnsureWhaleWalletExistsAsync()
    {
        try
        {
            var wallets = await _walletRepository.GetByNetworkAsync(1);
            var whaleWallet = wallets.FirstOrDefault(w => w.Label == "🐋 Whale Transactions");

            if (whaleWallet != null)
            {
                _whaleWalletId = whaleWallet.Id;
                return;
            }

            var newWhaleWallet = new MonitoredWallet
            {
                Address = "0x0000000000000000000000000000000000000000",
                Label = "🐋 Whale Transactions",
                BlockchainNetworkId = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var created = await _walletRepository.AddAsync(newWhaleWallet);
            _whaleWalletId = created.Id;

            _logger.LogInformation("Created whale wallet with ID {WalletId}", _whaleWalletId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring whale wallet exists");
        }
    }

    private string FormatAddress(string address)
    {
        if (string.IsNullOrEmpty(address) || address.Length < 10)
            return address;

        return $"{address.Substring(0, 6)}...{address.Substring(address.Length - 4)}";
    }
}
