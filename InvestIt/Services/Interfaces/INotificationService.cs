using InvestIt.Data.Entities;

namespace InvestIt.Services.Interfaces;

public interface INotificationService
{
    Task SendTransactionNotificationAsync(Transaction transaction);
}
