using InvestIt.Data;
using InvestIt.Data.Entities;
using InvestIt.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InvestIt.Repositories.Implementations;

public class NotificationQueueRepository : INotificationQueueRepository
{
    private readonly ApplicationDbContext _context;

    public NotificationQueueRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationQueue?> GetByIdAsync(int id)
    {
        return await _context.NotificationQueues
            .Include(n => n.Transaction)
            .ThenInclude(t => t.Wallet)
            .ThenInclude(w => w.BlockchainNetwork)
            .FirstOrDefaultAsync(n => n.Id == id);
    }

    public async Task<IEnumerable<NotificationQueue>> GetPendingAsync(int count = 100)
    {
        return await _context.NotificationQueues
            .Include(n => n.Transaction)
                .ThenInclude(t => t.Wallet)
                    .ThenInclude(w => w.BlockchainNetwork)
            .Where(n => n.Status == NotificationStatus.Pending)
            .OrderBy(n => n.CreatedAt)
            .Take(count)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<NotificationQueue> AddAsync(NotificationQueue notification)
    {
        _context.NotificationQueues.Add(notification);
        await _context.SaveChangesAsync();
        return notification;
    }

    public async Task UpdateAsync(NotificationQueue notification)
    {
        _context.NotificationQueues.Update(notification);
        await _context.SaveChangesAsync();
    }

    public async Task MarkAsProcessedAsync(int id)
    {
        var notification = await _context.NotificationQueues.FindAsync(id);
        if (notification != null)
        {
            notification.Status = NotificationStatus.Sent;
            notification.ProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAsFailedAsync(int id, string errorMessage)
    {
        var notification = await _context.NotificationQueues.FindAsync(id);
        if (notification != null)
        {
            notification.Status = NotificationStatus.Failed;
            notification.ErrorMessage = errorMessage;
            notification.RetryCount++;
            await _context.SaveChangesAsync();
        }
    }
}
