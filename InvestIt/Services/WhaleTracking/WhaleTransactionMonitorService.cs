using InvestIt.Data.Entities;
using InvestIt.Repositories.Interfaces;
using InvestIt.Services.ExplorerClients;
using InvestIt.Services.Interfaces;
using InvestIt.Services.PriceTracking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InvestIt.Services.WhaleTracking;

public class WhaleTransactionMonitorService
{
    private readonly EtherscanClient _etherscanClient;
    private readonly ICoinGeckoClient _priceClient;
    private readonly ITransactionProcessingService _transactionProcessor;
    private readonly IWalletRepository _walletRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WhaleTransactionMonitorService> _logger;

    private readonly decimal _whaleThresholdUsd;
    private int? _whaleWalletId;

    public WhaleTransactionMonitorService(
        EtherscanClient etherscanClient,
        ICoinGeckoClient priceClient,
        ITransactionProcessingService transactionProcessor,
        IWalletRepository walletRepository,
        IConfiguration configuration,
        ILogger<WhaleTransactionMonitorService> logger)
    {
        _etherscanClient = etherscanClient;
        _priceClient = priceClient;
        _transactionProcessor = transactionProcessor;
        _walletRepository = walletRepository;
        _configuration = configuration;
        _logger = logger;

        // Get threshold from config (default: $10,000)
        _whaleThresholdUsd = configuration.GetValue<decimal>("WhaleTracking:ThresholdUsd", 10000);
    }

    public async Task MonitorWhaleTransactionsAsync()
    {
        try
        {
            _logger.LogInformation("Starting whale transaction monitoring (threshold: ${Threshold})", _whaleThresholdUsd);

            // Ensure we have a virtual "whale" wallet to store these transactions
            await EnsureWhaleWalletExistsAsync();

            if (_whaleWalletId == null)
            {
                _logger.LogWarning("Could not create whale wallet, skipping monitoring");
                return;
            }

            // Get latest block transactions
            // Note: We'll monitor specific high-value wallets or use block explorer
            // For now, we'll scan some known DEX and whale addresses
            var knownWhaleAddresses = new[]
            {
                "0x28C6c06298d514Db089934071355E5743bf21d60", // Binance 14
                "0x21a31Ee1afC51d94C2eFcCAa2092aD1028285549", // Binance 15
                "0xDFd5293D8e347dFe59E90eFd55b2956a1343963d", // Binance 16
                "0x56Eddb7aa87536c09CCc2793473599fD21A8b17F", // Binance Hot Wallet
                "0x3f5CE5FBFe3E9af3971dD833D26bA9b5C936f0bE", // Binance Cold
                "0xD551234Ae421e3BCBA99A0Da6d736074f22192FF", // Binance
                "0x564286362092D8e7936f0549571a803B203aAceD", // Binance
                "0x0681d8Db095565FE8A346fA0277bFfdE9C0eDBBF", // Binance
                "0xfE9e8709d3215310075d67E3ed32A380CCf451C8", // Coinbase
                "0x503828976D22510aad0201ac7EC88293211D23Da", // Coinbase
                "0xddfAbCdc4D8FfC6d5beaf154f18B778f892A0740", // Coinbase
                "0x71660c4005BA85c37ccec55d0C4493E66Fe775d3", // Coinbase
                "0xA090e606E30bD747d4E6245a1517EbE430F0057e"  // Coinbase
            };

            var whaleCount = 0;

            // Fetch transactions from known whale addresses
            foreach (var address in knownWhaleAddresses)
            {
                try
                {
                    var transactions = await _etherscanClient.GetTransactionsAsync(address, limit: 10);

                    foreach (var tx in transactions)
                    {
                        // Calculate USD value
                        var usdValue = await CalculateTransactionValueUsdAsync(tx.TokenSymbol ?? "ETH", tx.Amount);

                        if (usdValue.HasValue && usdValue.Value >= _whaleThresholdUsd)
                        {
                            // This is a whale transaction!
                            _logger.LogInformation(
                                "🐋 WHALE DETECTED: {Hash} - {Amount} {Token} (${Usd:N0})",
                                tx.TxHash, tx.Amount, tx.TokenSymbol, usdValue.Value);

                            // Store it
                            await _transactionProcessor.ProcessAndStoreAsync(
                                tx,
                                _whaleWalletId.Value,
                                "WHALE_MONITOR" // Special marker
                            );

                            whaleCount++;
                        }
                    }

                    // Rate limit - don't hammer the API
                    await Task.Delay(300); // 3 requests per second
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error monitoring whale address {Address}", address);
                }
            }

            _logger.LogInformation("Whale monitoring complete. Found {Count} whale transactions", whaleCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in whale transaction monitoring");
        }
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
            // Check if whale wallet already exists
            var wallets = await _walletRepository.GetByNetworkAsync(1); // Ethereum
            var whaleWallet = wallets.FirstOrDefault(w => w.Label == "🐋 Whale Transactions");

            if (whaleWallet != null)
            {
                _whaleWalletId = whaleWallet.Id;
                return;
            }

            // Create virtual whale wallet
            var newWhaleWallet = new MonitoredWallet
            {
                Address = "0x0000000000000000000000000000000000000000", // Zero address (placeholder)
                Label = "🐋 Whale Transactions",
                BlockchainNetworkId = 1, // Ethereum
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
}
