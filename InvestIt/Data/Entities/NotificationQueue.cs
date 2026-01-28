namespace InvestIt.Data.Entities;

public class NotificationQueue
{
    public int Id { get; set; }
    public int TransactionId { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; } = 0;
    public string? ErrorMessage { get; set; }

    // Navigation properties
    public Transaction Transaction { get; set; } = null!;
}

public enum NotificationStatus
{
    Pending,
    Sent,
    Failed
}
