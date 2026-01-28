namespace InvestIt.Data.Entities;

public class BlockchainNetwork
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string ChainId { get; set; }
    public required string ApiUrl { get; set; }
    public string? ApiKey { get; set; }
    public int RateLimitPerSecond { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<MonitoredWallet> MonitoredWallets { get; set; } = new List<MonitoredWallet>();
    public ICollection<MonitoringStatus> MonitoringStatuses { get; set; } = new List<MonitoringStatus>();
    public ICollection<ApiRateLimitTracking> ApiRateLimitTrackings { get; set; } = new List<ApiRateLimitTracking>();
}
