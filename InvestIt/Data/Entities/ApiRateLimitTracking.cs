namespace InvestIt.Data.Entities;

public class ApiRateLimitTracking
{
    public int Id { get; set; }
    public int NetworkId { get; set; }
    public int TokensAvailable { get; set; }
    public DateTime LastRefreshTime { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public BlockchainNetwork Network { get; set; } = null!;
}
