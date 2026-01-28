namespace InvestIt.Services.ExplorerClients.Models;

/// <summary>
/// Normalized transaction model used across all blockchains
/// </summary>
public class BlockchainTransaction
{
    public required string TxHash { get; set; }
    public required string FromAddress { get; set; }
    public required string ToAddress { get; set; }
    public string? TokenSymbol { get; set; }
    public string? TokenAddress { get; set; }
    public decimal Amount { get; set; }
    public DateTime Timestamp { get; set; }
    public long BlockNumber { get; set; }
    public decimal? GasUsed { get; set; }
    public decimal? GasPrice { get; set; }
    public string? RawData { get; set; }
}
