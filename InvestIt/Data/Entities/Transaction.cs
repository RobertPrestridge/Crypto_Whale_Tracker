namespace InvestIt.Data.Entities;

public class Transaction
{
    public int Id { get; set; }
    public required string TxHash { get; set; }
    public int WalletId { get; set; }
    public required string FromAddress { get; set; }
    public required string ToAddress { get; set; }
    public string? TokenSymbol { get; set; }
    public string? TokenAddress { get; set; }
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public DateTime Timestamp { get; set; }
    public long BlockNumber { get; set; }
    public decimal? GasUsed { get; set; }
    public decimal? GasPrice { get; set; }
    public string? RawData { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public MonitoredWallet Wallet { get; set; } = null!;
    public ICollection<NotificationQueue> NotificationQueues { get; set; } = new List<NotificationQueue>();
}

public enum TransactionType
{
    Buy,
    Sell,
    Transfer
}
