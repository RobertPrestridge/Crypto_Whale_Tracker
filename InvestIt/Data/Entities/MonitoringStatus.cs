namespace InvestIt.Data.Entities;

public class MonitoringStatus
{
    public int Id { get; set; }
    public int NetworkId { get; set; }
    public DateTime? LastPollTime { get; set; }
    public bool IsHealthy { get; set; } = true;
    public int ErrorCount { get; set; } = 0;
    public string? LastErrorMessage { get; set; }
    public DateTime? LastErrorTime { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public BlockchainNetwork Network { get; set; } = null!;
}
