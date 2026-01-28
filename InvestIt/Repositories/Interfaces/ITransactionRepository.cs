using InvestIt.Data.Entities;

namespace InvestIt.Repositories.Interfaces;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(int id);
    Task<Transaction?> GetByTxHashAsync(string txHash);
    Task<IEnumerable<Transaction>> GetRecentAsync(int count = 50);
    Task<IEnumerable<Transaction>> GetByWalletAsync(int walletId, int skip = 0, int take = 50);
    Task<IEnumerable<Transaction>> GetByTokenSymbolAsync(string tokenSymbol, int skip = 0, int take = 50);
    Task<IEnumerable<Transaction>> GetByTypeAsync(TransactionType type, int skip = 0, int take = 50);
    Task<IEnumerable<Transaction>> GetWhaleTransactionsAsync(int count = 50);
    Task<IEnumerable<Transaction>> GetRecentWithWhalePriorityAsync(int count = 50);
    Task<Transaction> AddAsync(Transaction transaction);
    Task<bool> ExistsByHashAsync(string txHash);
    Task<int> GetCountByWalletAsync(int walletId);
    Task<int> GetWhaleTransactionCountAsync();
    Task<int> GetTransactionCountByDateAsync(DateTime date);
}
