using InvestIt.Services.ExplorerClients.Models;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace InvestIt.Services.ExplorerClients;

public class EtherscanClient : IBlockchainExplorerClient
{
    private readonly IEtherscanApi _api;
    private readonly string? _apiKey;
    private readonly int _chainId;
    private readonly ILogger<EtherscanClient> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public string NetworkName => "Ethereum";

    public EtherscanClient(
        IEtherscanApi api,
        IConfiguration configuration,
        ILogger<EtherscanClient> logger)
    {
        _api = api;
        _apiKey = configuration["BlockchainApis:Ethereum:ApiKey"];
        _chainId = configuration.GetValue<int>("BlockchainApis:Ethereum:ChainId", 1);
        _logger = logger;

        // Configure retry policy: 3 attempts with exponential backoff
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Retry {RetryCount} for Etherscan API after {TotalSeconds}s due to: {Message}",
                        retryCount, timeSpan.TotalSeconds, exception.Message);
                });
    }

    public async Task<IEnumerable<BlockchainTransaction>> GetTransactionsAsync(string address, int limit = 100)
    {
        try
        {
            var transactions = new List<BlockchainTransaction>();

            // Fetch normal transactions
            var normalTxsResponse = await _retryPolicy.ExecuteAsync(() =>
                _api.GetNormalTransactionsAsync(
                    module: "account",
                    action: "txlist",
                    address: address,
                    startBlock: 0,
                    endBlock: 99999999,
                    page: 1,
                    offset: limit,
                    sort: "desc",
                    apiKey: _apiKey,
                    chainId: _chainId));

            if (normalTxsResponse.Status == "1" && !normalTxsResponse.Result.IsError && normalTxsResponse.Result.Transactions != null)
            {
                transactions.AddRange(normalTxsResponse.Result.Transactions.Select(MapToBlockchainTransaction));
            }
            else if (normalTxsResponse.Result.IsError)
            {
                _logger.LogWarning("Etherscan API error for {Address}: {Error}", address, normalTxsResponse.Result.ErrorMessage);
            }

            // Fetch ERC-20 token transactions
            var tokenTxsResponse = await _retryPolicy.ExecuteAsync(() =>
                _api.GetTokenTransactionsAsync(
                    module: "account",
                    action: "tokentx",
                    address: address,
                    startBlock: 0,
                    endBlock: 99999999,
                    page: 1,
                    offset: limit,
                    sort: "desc",
                    apiKey: _apiKey,
                    chainId: _chainId));

            if (tokenTxsResponse.Status == "1" && !tokenTxsResponse.Result.IsError && tokenTxsResponse.Result.Transactions != null)
            {
                transactions.AddRange(tokenTxsResponse.Result.Transactions.Select(MapToBlockchainTransaction));
            }
            else if (tokenTxsResponse.Result.IsError)
            {
                _logger.LogWarning("Etherscan API token TX error for {Address}: {Error}", address, tokenTxsResponse.Result.ErrorMessage);
            }

            // Sort by timestamp descending and limit
            return transactions
                .OrderByDescending(t => t.Timestamp)
                .Take(limit)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching transactions from Etherscan for address {Address}", address);
            return Enumerable.Empty<BlockchainTransaction>();
        }
    }

    // Max value - using 10^15 to be very safe
    private const decimal MaxDatabaseDecimal = 1000000000000000m;

    private static decimal SafeParseDecimal(string? value, int decimals = 0)
    {
        if (string.IsNullOrEmpty(value)) return 0;

        try
        {
            if (!decimal.TryParse(value, out var val)) return 0;
            if (val < 0) val = 0; // No negative values

            // Apply decimal division if needed
            if (decimals > 0)
            {
                decimals = Math.Clamp(decimals, 0, 18);
                var divisor = (decimal)Math.Pow(10, decimals);
                val /= divisor;
            }

            // Clamp to database-safe maximum
            return Math.Min(val, MaxDatabaseDecimal);
        }
        catch (OverflowException)
        {
            return MaxDatabaseDecimal;
        }
    }

    private BlockchainTransaction MapToBlockchainTransaction(EtherscanTransaction tx)
    {
        var timestamp = long.TryParse(tx.TimeStamp, out var ts)
            ? DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime
            : DateTime.UtcNow;

        // Safe token decimal parsing
        var tokenDecimals = 18;
        if (!string.IsNullOrEmpty(tx.TokenDecimal) && int.TryParse(tx.TokenDecimal, out var td))
        {
            tokenDecimals = Math.Clamp(td, 0, 18);
        }

        // Safe amount parsing with overflow protection
        var amount = SafeParseDecimal(tx.Value, tokenDecimals);

        var blockNumber = long.TryParse(tx.BlockNumber, out var bn) ? bn : 0;

        // GasUsed is in gas units (no conversion needed, but still clamp)
        var gasUsed = SafeParseDecimal(tx.GasUsed);

        // GasPrice in Gwei (divide by 10^9)
        var gasPrice = SafeParseDecimal(tx.GasPrice, 9);

        return new BlockchainTransaction
        {
            TxHash = tx.Hash,
            FromAddress = tx.From,
            ToAddress = tx.To ?? string.Empty,
            TokenSymbol = string.IsNullOrEmpty(tx.TokenSymbol) ? "ETH" : tx.TokenSymbol,
            TokenAddress = tx.ContractAddress,
            Amount = amount,
            Timestamp = timestamp,
            BlockNumber = blockNumber,
            GasUsed = gasUsed > 0 ? gasUsed : null,
            GasPrice = gasPrice > 0 ? gasPrice : null
        };
    }
}
