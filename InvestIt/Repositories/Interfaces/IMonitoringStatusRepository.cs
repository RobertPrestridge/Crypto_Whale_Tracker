using InvestIt.Data.Entities;

namespace InvestIt.Repositories.Interfaces;

public interface IMonitoringStatusRepository
{
    Task<MonitoringStatus?> GetByNetworkIdAsync(int networkId);
    Task<IEnumerable<MonitoringStatus>> GetAllAsync();
    Task UpsertAsync(MonitoringStatus status);
    Task UpdateHealthAsync(int networkId, bool isHealthy, string? errorMessage = null);
}
