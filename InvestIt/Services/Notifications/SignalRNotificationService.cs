using InvestIt.Data.Entities;
using InvestIt.Helpers;
using InvestIt.Hubs;
using InvestIt.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace InvestIt.Services.Notifications;

public class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<TransactionNotificationHub> _hubContext;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(
        IHubContext<TransactionNotificationHub> hubContext,
        ILogger<SignalRNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendTransactionNotificationAsync(Transaction transaction)
    {
        try
        {
            if (transaction == null)
            {
                _logger.LogWarning("Attempted to send notification for null transaction");
                return;
            }

            var notification = new
            {
                transactionId = transaction.Id,
                txHash = transaction.TxHash,
                walletId = transaction.WalletId,
                walletAddress = transaction.Wallet?.Address ?? "Unknown",
                walletLabel = transaction.Wallet?.Label ?? "",
                networkName = transaction.Wallet?.BlockchainNetwork?.Name ?? "Unknown",
                type = transaction.Type.ToString(),
                tokenSymbol = transaction.TokenSymbol ?? "Unknown",
                amount = transaction.Amount,
                fromAddress = transaction.FromAddress ?? "",
                toAddress = transaction.ToAddress ?? "",
                timestamp = TimeZoneHelper.ToCentralTime(transaction.Timestamp),
                blockNumber = transaction.BlockNumber,
                message = FormatNotificationMessage(transaction)
            };

            _logger.LogDebug(
                "Sending SignalR notification: TxHash={TxHash}, Type={Type}, Amount={Amount}, Network={Network}",
                transaction.TxHash, transaction.Type, transaction.Amount, notification.networkName);

            // Send to all connected clients
            await _hubContext.Clients.All.SendAsync("ReceiveTransaction", notification);

            // Also send to specific wallet group if needed
            await _hubContext.Clients
                .Group($"wallet_{transaction.WalletId}")
                .SendAsync("ReceiveWalletTransaction", notification);

            _logger.LogInformation(
                "Sent SignalR notification for transaction {TxHash} ({Type}) to all clients",
                transaction.TxHash, transaction.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SignalR notification for transaction {TxHash}", transaction.TxHash);
            throw;
        }
    }

    private string FormatNotificationMessage(Transaction transaction)
    {
        var action = transaction.Type switch
        {
            TransactionType.Buy => "received",
            TransactionType.Sell => "sent",
            _ => "transferred"
        };

        return $"Wallet {action} {transaction.Amount:N4} {transaction.TokenSymbol} on {transaction.Wallet?.BlockchainNetwork?.Name ?? "Unknown Network"}";
    }
}
