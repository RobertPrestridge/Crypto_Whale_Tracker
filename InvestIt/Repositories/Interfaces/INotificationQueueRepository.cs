using InvestIt.Data.Entities;

namespace InvestIt.Repositories.Interfaces;

public interface INotificationQueueRepository
{
    Task<NotificationQueue?> GetByIdAsync(int id);
    Task<IEnumerable<NotificationQueue>> GetPendingAsync(int count = 100);
    Task<NotificationQueue> AddAsync(NotificationQueue notification);
    Task UpdateAsync(NotificationQueue notification);
    Task MarkAsProcessedAsync(int id);
    Task MarkAsFailedAsync(int id, string errorMessage);
}
