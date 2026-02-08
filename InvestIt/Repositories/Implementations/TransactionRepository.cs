using InvestIt.Data;
using InvestIt.Data.Entities;
using InvestIt.Helpers;
using InvestIt.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InvestIt.Repositories.Implementations;

public class TransactionRepository : ITransactionRepository
{
    private readonly ApplicationDbContext _context;

    public TransactionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Transaction?> GetByIdAsync(int id)
    {
        return await _context.Transactions
            .Include(t => t.Wallet)
            .ThenInclude(w => w.BlockchainNetwork)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Transaction?> GetByTxHashAsync(string txHash)
    {
        return await _context.Transactions
            .Include(t => t.Wallet)
            .FirstOrDefaultAsync(t => t.TxHash == txHash);
    }

    public async Task<IEnumerable<Transaction>> GetRecentAsync(int count = 50)
    {
        return await _context.Transactions
            .Include(t => t.Wallet)
            .ThenInclude(w => w.BlockchainNetwork)
            .OrderByDescending(t => t.Timestamp)
            .Take(count)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetByWalletAsync(int walletId, int skip = 0, int take = 50)
    {
        return await _context.Transactions
            .Where(t => t.WalletId == walletId)
            .OrderByDescending(t => t.Timestamp)
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetByTokenSymbolAsync(string tokenSymbol, int skip = 0, int take = 50)
    {
        return await _context.Transactions
            .Include(t => t.Wallet)
            .ThenInclude(w => w.BlockchainNetwork)
            .Where(t => t.TokenSymbol == tokenSymbol)
            .OrderByDescending(t => t.Timestamp)
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetByTypeAsync(TransactionType type, int skip = 0, int take = 50)
    {
        return await _context.Transactions
            .Include(t => t.Wallet)
            .ThenInclude(w => w.BlockchainNetwork)
            .Where(t => t.Type == type)
            .OrderByDescending(t => t.Timestamp)
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Transaction> AddAsync(Transaction transaction)
    {
        // Use FormattableString for raw SQL to avoid EF Core parameter type issues
        FormattableString sql = $@"
            INSERT INTO Transactions
                (TxHash, WalletId, FromAddress, ToAddress, TokenSymbol, TokenAddress, Amount, AmountUsd, Type, Timestamp, BlockNumber, GasUsed, GasPrice, RawData, CreatedAt)
            VALUES
                ({transaction.TxHash}, {transaction.WalletId}, {transaction.FromAddress}, {transaction.ToAddress},
                 {transaction.TokenSymbol}, {transaction.TokenAddress}, {transaction.Amount}, {transaction.AmountUsd}, {(int)transaction.Type},
                 {transaction.Timestamp}, {transaction.BlockNumber}, {transaction.GasUsed}, {transaction.GasPrice},
                 {transaction.RawData}, {transaction.CreatedAt})";

        await _context.Database.ExecuteSqlAsync(sql);

        // Fetch the inserted transaction
        var inserted = await _context.Transactions
            .FirstOrDefaultAsync(t => t.TxHash == transaction.TxHash);

        return inserted ?? transaction;
    }

    public async Task<bool> ExistsByHashAsync(string txHash)
    {
        return await _context.Transactions.AnyAsync(t => t.TxHash == txHash);
    }

    public async Task<int> GetCountByWalletAsync(int walletId)
    {
        return await _context.Transactions.CountAsync(t => t.WalletId == walletId);
    }

    public async Task<IEnumerable<Transaction>> GetRecentAboveUsdAsync(decimal minAmountUsd, int count = 50)
    {
        return await _context.Transactions
            .Include(t => t.Wallet)
            .ThenInclude(w => w.BlockchainNetwork)
            .Where(t => t.AmountUsd != null && t.AmountUsd >= minAmountUsd)
            .OrderByDescending(t => t.Timestamp)
            .Take(count)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetWhaleTransactionsAsync(int count = 50)
    {
        return await _context.Transactions
            .Include(t => t.Wallet)
            .ThenInclude(w => w.BlockchainNetwork)
            .Where(t => t.Wallet.Label != null && t.Wallet.Label.Contains("Whale"))
            .OrderByDescending(t => t.Timestamp)
            .Take(count)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetRecentWithWhalePriorityAsync(int count = 50)
    {
        // Get whale transactions first, then other transactions, ordered by timestamp within each group
        var whaleTransactions = await _context.Transactions
            .Include(t => t.Wallet)
            .ThenInclude(w => w.BlockchainNetwork)
            .Where(t => t.Wallet.Label != null && t.Wallet.Label.Contains("Whale"))
            .OrderByDescending(t => t.Timestamp)
            .AsNoTracking()
            .ToListAsync();

        var otherTransactions = await _context.Transactions
            .Include(t => t.Wallet)
            .ThenInclude(w => w.BlockchainNetwork)
            .Where(t => t.Wallet.Label == null || !t.Wallet.Label.Contains("Whale"))
            .OrderByDescending(t => t.Timestamp)
            .AsNoTracking()
            .ToListAsync();

        // Combine with whales first, then others
        return whaleTransactions.Concat(otherTransactions).Take(count);
    }

    public async Task<int> GetWhaleTransactionCountAsync()
    {
        return await _context.Transactions
            .Where(t => t.Wallet.Label != null && t.Wallet.Label.Contains("Whale"))
            .CountAsync();
    }

    public async Task<int> GetWhaleTransactionCountByUsdAsync(decimal minAmountUsd)
    {
        return await _context.Transactions
            .Where(t => t.AmountUsd != null && t.AmountUsd >= minAmountUsd)
            .CountAsync();
    }

    public async Task<int> GetTransactionCountByDateAsync(DateTime date)
    {
        // Convert Central time date boundaries to UTC for database query
        var startOfDayUtc = TimeZoneHelper.GetTodayStartUtc();
        var endOfDayUtc = TimeZoneHelper.GetTodayEndUtc();

        return await _context.Transactions
            .CountAsync(t => t.Timestamp >= startOfDayUtc && t.Timestamp < endOfDayUtc);
    }
}
