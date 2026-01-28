using InvestIt.Data;
using InvestIt.Data.Entities;
using InvestIt.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InvestIt.Repositories.Implementations;

public class MonitoringStatusRepository : IMonitoringStatusRepository
{
    private readonly ApplicationDbContext _context;

    public MonitoringStatusRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MonitoringStatus?> GetByNetworkIdAsync(int networkId)
    {
        return await _context.MonitoringStatuses
            .Include(s => s.Network)
            .FirstOrDefaultAsync(s => s.NetworkId == networkId);
    }

    public async Task<IEnumerable<MonitoringStatus>> GetAllAsync()
    {
        return await _context.MonitoringStatuses
            .Include(s => s.Network)
            .ToListAsync();
    }

    public async Task UpsertAsync(MonitoringStatus status)
    {
        var existing = await _context.MonitoringStatuses
            .FirstOrDefaultAsync(s => s.NetworkId == status.NetworkId);

        if (existing != null)
        {
            existing.LastPollTime = status.LastPollTime;
            existing.IsHealthy = status.IsHealthy;
            existing.ErrorCount = status.ErrorCount;
            existing.LastErrorMessage = status.LastErrorMessage;
            existing.LastErrorTime = status.LastErrorTime;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.MonitoringStatuses.Add(status);
        }

        await _context.SaveChangesAsync();
    }

    public async Task UpdateHealthAsync(int networkId, bool isHealthy, string? errorMessage = null)
    {
        var status = await _context.MonitoringStatuses
            .FirstOrDefaultAsync(s => s.NetworkId == networkId);

        if (status != null)
        {
            status.IsHealthy = isHealthy;
            if (!isHealthy)
            {
                status.ErrorCount++;
                status.LastErrorMessage = errorMessage;
                status.LastErrorTime = DateTime.UtcNow;
            }
            else
            {
                status.ErrorCount = 0;
            }
            status.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
