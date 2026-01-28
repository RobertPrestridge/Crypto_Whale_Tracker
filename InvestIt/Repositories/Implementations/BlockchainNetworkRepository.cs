using InvestIt.Data;
using InvestIt.Data.Entities;
using InvestIt.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InvestIt.Repositories.Implementations;

public class BlockchainNetworkRepository : IBlockchainNetworkRepository
{
    private readonly ApplicationDbContext _context;

    public BlockchainNetworkRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BlockchainNetwork?> GetByIdAsync(int id)
    {
        return await _context.BlockchainNetworks.FindAsync(id);
    }

    public async Task<BlockchainNetwork?> GetByNameAsync(string name)
    {
        return await _context.BlockchainNetworks
            .FirstOrDefaultAsync(n => n.Name == name);
    }

    public async Task<BlockchainNetwork?> GetByChainIdAsync(string chainId)
    {
        return await _context.BlockchainNetworks
            .FirstOrDefaultAsync(n => n.ChainId == chainId);
    }

    public async Task<IEnumerable<BlockchainNetwork>> GetAllAsync()
    {
        return await _context.BlockchainNetworks
            .OrderBy(n => n.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<BlockchainNetwork>> GetActiveAsync()
    {
        return await _context.BlockchainNetworks
            .Where(n => n.IsActive)
            .OrderBy(n => n.Name)
            .ToListAsync();
    }

    public async Task UpdateAsync(BlockchainNetwork network)
    {
        _context.BlockchainNetworks.Update(network);
        await _context.SaveChangesAsync();
    }
}
