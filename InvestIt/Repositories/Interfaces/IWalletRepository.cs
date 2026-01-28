using InvestIt.Data.Entities;

namespace InvestIt.Repositories.Interfaces;

public interface IWalletRepository
{
    Task<MonitoredWallet?> GetByIdAsync(int id);
    Task<MonitoredWallet?> GetByAddressAsync(string address, int networkId);
    Task<IEnumerable<MonitoredWallet>> GetAllAsync();
    Task<IEnumerable<MonitoredWallet>> GetActiveAsync();
    Task<IEnumerable<MonitoredWallet>> GetByNetworkAsync(int networkId);
    Task<MonitoredWallet> AddAsync(MonitoredWallet wallet);
    Task UpdateAsync(MonitoredWallet wallet);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(string address, int networkId);
}
