namespace InvestIt.Data.Entities;

public class MonitoredWallet
{
    public int Id { get; set; }
    public required string Address { get; set; }
    public int BlockchainNetworkId { get; set; }
    public string? Label { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastCheckedAt { get; set; }

    // Navigation properties
    public BlockchainNetwork BlockchainNetwork { get; set; } = null!;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
