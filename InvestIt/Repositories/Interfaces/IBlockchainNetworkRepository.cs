using InvestIt.Data.Entities;

namespace InvestIt.Repositories.Interfaces;

public interface IBlockchainNetworkRepository
{
    Task<BlockchainNetwork?> GetByIdAsync(int id);
    Task<BlockchainNetwork?> GetByNameAsync(string name);
    Task<BlockchainNetwork?> GetByChainIdAsync(string chainId);
    Task<IEnumerable<BlockchainNetwork>> GetAllAsync();
    Task<IEnumerable<BlockchainNetwork>> GetActiveAsync();
    Task UpdateAsync(BlockchainNetwork network);
}
