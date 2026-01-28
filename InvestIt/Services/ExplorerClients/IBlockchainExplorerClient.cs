using InvestIt.Services.ExplorerClients.Models;

namespace InvestIt.Services.ExplorerClients;

public interface IBlockchainExplorerClient
{
    Task<IEnumerable<BlockchainTransaction>> GetTransactionsAsync(string address, int limit = 100);
    string NetworkName { get; }
}
