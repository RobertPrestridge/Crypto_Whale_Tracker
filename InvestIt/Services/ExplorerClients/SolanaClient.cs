using InvestIt.Services.ExplorerClients.Models;
using Microsoft.Extensions.Logging;

namespace InvestIt.Services.ExplorerClients;

/// <summary>
/// Solana blockchain client - placeholder implementation
/// In production, integrate with Solscan API or use Solnet library
/// </summary>
public class SolanaClient : IBlockchainExplorerClient
{
    private readonly ILogger<SolanaClient> _logger;

    public string NetworkName => "Solana";

    public SolanaClient(ILogger<SolanaClient> logger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<BlockchainTransaction>> GetTransactionsAsync(string address, int limit = 100)
    {
        // Placeholder implementation
        // In production: Use Solnet.Rpc to connect to Solana RPC or integrate Solscan API
        _logger.LogInformation("Fetching Solana transactions for {Address} (placeholder implementation)", address);

        await Task.CompletedTask;
        return Enumerable.Empty<BlockchainTransaction>();
    }
}
