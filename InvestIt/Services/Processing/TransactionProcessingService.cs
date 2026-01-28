using InvestIt.Data.Entities;
using InvestIt.Repositories.Interfaces;
using InvestIt.Services.ExplorerClients.Models;
using InvestIt.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace InvestIt.Services.Processing;

public class TransactionProcessingService : ITransactionProcessingService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly INotificationQueueRepository _notificationRepository;
    private readonly ILogger<TransactionProcessingService> _logger;

    // Max value - using a conservative limit to avoid any precision issues
    // Using 10^15 to be very safe (1 quadrillion)
    private const decimal MaxDatabaseDecimal = 1000000000000000m;

    // Minimum transaction amount threshold to filter out dust transactions
    private const decimal MinimumTransactionAmount = 0.0001m;

    public TransactionProcessingService(
        ITransactionRepository transactionRepository,
        INotificationQueueRepository notificationRepository,
        ILogger<TransactionProcessingService> logger)
    {
        _transactionRepository = transactionRepository;
        _notificationRepository = notificationRepository;
        _logger = logger;
    }

    /// <summary>
    /// Clamp a decimal value to fit within SQL Server decimal(38,18) bounds
    /// </summary>
    private static decimal ClampToDbSafe(decimal value)
    {
        if (value < 0) return 0;
        return Math.Min(value, MaxDatabaseDecimal);
    }

    private static decimal? ClampToDbSafe(decimal? value)
    {
        if (!value.HasValue) return null;
        var clamped = ClampToDbSafe(value.Value);
        return clamped > 0 ? clamped : null;
    }

    public Transaction ProcessTransaction(BlockchainTransaction blockchainTx, int walletId, string monitoredWalletAddress)
    {
        // Classify transaction type based on address matching
        var txType = ClassifyTransaction(blockchainTx, monitoredWalletAddress);

        // Clamp all decimal values to database-safe range to prevent overflow
        var transaction = new Transaction
        {
            TxHash = blockchainTx.TxHash,
            WalletId = walletId,
            FromAddress = blockchainTx.FromAddress,
            ToAddress = blockchainTx.ToAddress,
            TokenSymbol = blockchainTx.TokenSymbol,
            TokenAddress = blockchainTx.TokenAddress,
            Amount = ClampToDbSafe(blockchainTx.Amount),
            Type = txType,
            Timestamp = blockchainTx.Timestamp,
            BlockNumber = blockchainTx.BlockNumber,
            GasUsed = ClampToDbSafe(blockchainTx.GasUsed),
            GasPrice = ClampToDbSafe(blockchainTx.GasPrice),
            RawData = blockchainTx.RawData,
            CreatedAt = DateTime.UtcNow
        };

        return transaction;
    }

    public async Task<Transaction?> ProcessAndStoreAsync(
        BlockchainTransaction blockchainTx,
        int walletId,
        string monitoredWalletAddress)
    {
        try
        {
            // Check if transaction already exists (deduplication)
            if (await _transactionRepository.ExistsByHashAsync(blockchainTx.TxHash))
            {
                _logger.LogDebug("Transaction {TxHash} already exists, skipping", blockchainTx.TxHash);
                return null;
            }

            // Skip transactions with amount below minimum threshold
            if (blockchainTx.Amount < MinimumTransactionAmount)
            {
                _logger.LogDebug("Transaction {TxHash} amount {Amount} below minimum threshold, skipping",
                    blockchainTx.TxHash, blockchainTx.Amount);
                return null;
            }

            // Process and classify transaction
            var transaction = ProcessTransaction(blockchainTx, walletId, monitoredWalletAddress);

            // Store transaction
            var storedTransaction = await _transactionRepository.AddAsync(transaction);

            _logger.LogInformation(
                "Processed {Type} transaction {TxHash} for wallet {WalletId}: {Amount} {Symbol}",
                transaction.Type, transaction.TxHash, walletId, transaction.Amount, transaction.TokenSymbol);

            // Queue notification
            await QueueNotificationAsync(storedTransaction.Id);

            return storedTransaction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing transaction {TxHash}", blockchainTx.TxHash);
            return null;
        }
    }

    private TransactionType ClassifyTransaction(BlockchainTransaction tx, string monitoredWalletAddress)
    {
        // Normalize addresses for comparison (case-insensitive)
        var normalizedMonitoredAddress = monitoredWalletAddress.ToLowerInvariant();
        var normalizedFromAddress = tx.FromAddress.ToLowerInvariant();
        var normalizedToAddress = tx.ToAddress.ToLowerInvariant();

        // If wallet is receiving tokens -> BUY
        if (normalizedToAddress == normalizedMonitoredAddress)
        {
            return TransactionType.Buy;
        }

        // If wallet is sending tokens -> SELL
        if (normalizedFromAddress == normalizedMonitoredAddress)
        {
            return TransactionType.Sell;
        }

        // Otherwise, it's a transfer (shouldn't normally happen for monitored wallets)
        return TransactionType.Transfer;
    }

    private async Task QueueNotificationAsync(int transactionId)
    {
        try
        {
            var notification = new NotificationQueue
            {
                TransactionId = transactionId,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            await _notificationRepository.AddAsync(notification);
            _logger.LogDebug("Queued notification for transaction {TransactionId}", transactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queueing notification for transaction {TransactionId}", transactionId);
        }
    }
}
