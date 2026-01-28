using InvestIt.Data;
using InvestIt.Data.Entities;
using InvestIt.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InvestIt.Repositories.Implementations;

public class WalletRepository : IWalletRepository
{
    private readonly ApplicationDbContext _context;

    public WalletRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MonitoredWallet?> GetByIdAsync(int id)
    {
        return await _context.MonitoredWallets
            .Include(w => w.BlockchainNetwork)
            .FirstOrDefaultAsync(w => w.Id == id);
    }

    public async Task<MonitoredWallet?> GetByAddressAsync(string address, int networkId)
    {
        return await _context.MonitoredWallets
            .Include(w => w.BlockchainNetwork)
            .FirstOrDefaultAsync(w => w.Address == address && w.BlockchainNetworkId == networkId);
    }

    public async Task<IEnumerable<MonitoredWallet>> GetAllAsync()
    {
        return await _context.MonitoredWallets
            .Include(w => w.BlockchainNetwork)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<MonitoredWallet>> GetActiveAsync()
    {
        return await _context.MonitoredWallets
            .Include(w => w.BlockchainNetwork)
            .Where(w => w.IsActive)
            .ToListAsync();
    }

    public async Task<IEnumerable<MonitoredWallet>> GetByNetworkAsync(int networkId)
    {
        return await _context.MonitoredWallets
            .Include(w => w.BlockchainNetwork)
            .Where(w => w.BlockchainNetworkId == networkId)
            .ToListAsync();
    }

    public async Task<MonitoredWallet> AddAsync(MonitoredWallet wallet)
    {
        _context.MonitoredWallets.Add(wallet);
        await _context.SaveChangesAsync();
        return wallet;
    }

    public async Task UpdateAsync(MonitoredWallet wallet)
    {
        _context.MonitoredWallets.Update(wallet);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var wallet = await _context.MonitoredWallets.FindAsync(id);
        if (wallet != null)
        {
            _context.MonitoredWallets.Remove(wallet);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(string address, int networkId)
    {
        return await _context.MonitoredWallets
            .AnyAsync(w => w.Address == address && w.BlockchainNetworkId == networkId);
    }
}
