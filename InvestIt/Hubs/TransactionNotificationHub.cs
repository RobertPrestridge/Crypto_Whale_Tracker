using InvestIt.Helpers;
using Microsoft.AspNetCore.SignalR;

namespace InvestIt.Hubs;

public class TransactionNotificationHub : Hub
{
    private readonly ILogger<TransactionNotificationHub> _logger;

    public TransactionNotificationHub(ILogger<TransactionNotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("SignalR client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(
            "SignalR client disconnected: {ConnectionId}. Reason: {Reason}",
            Context.ConnectionId,
            exception?.Message ?? "Normal disconnect");
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SubscribeToWallet(int walletId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"wallet_{walletId}");
        _logger.LogInformation(
            "Client {ConnectionId} subscribed to wallet {WalletId}",
            Context.ConnectionId, walletId);
    }

    public async Task UnsubscribeFromWallet(int walletId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"wallet_{walletId}");
        _logger.LogInformation(
            "Client {ConnectionId} unsubscribed from wallet {WalletId}",
            Context.ConnectionId, walletId);
    }

    /// <summary>
    /// Test method to verify SignalR connection is working
    /// </summary>
    public async Task Ping()
    {
        _logger.LogInformation("Ping received from client {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("Pong", new { message = "Connection verified", timestamp = TimeZoneHelper.ToCentralTime(DateTime.UtcNow) });
    }

    /// <summary>
    /// Sends a test transaction notification to the caller for debugging
    /// </summary>
    public async Task SendTestNotification()
    {
        _logger.LogInformation("Test notification requested by client {ConnectionId}", Context.ConnectionId);

        var testNotification = new
        {
            transactionId = 0,
            txHash = "0xTEST" + Guid.NewGuid().ToString("N")[..8],
            walletId = 0,
            walletAddress = "0xTestWallet",
            walletLabel = "Test Wallet",
            networkName = "Ethereum",
            type = "Buy",
            tokenSymbol = "ETH",
            amount = 1.2345m,
            fromAddress = "0xSender",
            toAddress = "0xTestWallet",
            timestamp = TimeZoneHelper.ToCentralTime(DateTime.UtcNow),
            blockNumber = 12345678L,
            message = "Test notification - SignalR is working!"
        };

        await Clients.Caller.SendAsync("ReceiveTransaction", testNotification);
        _logger.LogInformation("Test notification sent to client {ConnectionId}", Context.ConnectionId);
    }
}
