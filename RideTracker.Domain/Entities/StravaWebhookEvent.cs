namespace RideTracker.Domain.Entities;

public class StravaWebhookEvent
{
    public long Id { get; set; }
    public long SubscriptionId { get; set; }
    public string ObjectType { get; set; } = string.Empty;
    public long ObjectId { get; set; }
    public string AspectType { get; set; } = string.Empty;
    public long OwnerId { get; set; }
    public DateTime EventTime { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string Status { get; set; } = "pending";
    public string? ErrorMessage { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
}
