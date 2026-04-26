namespace RideTracker.Application.Interfaces;

public interface IStravaWebhookService
{
    string VerifyToken { get; }
    Task<long> CreateSubscriptionAsync();
    Task<StravaSubscriptionInfo?> GetSubscriptionAsync();
    Task DeleteSubscriptionAsync(long id);
}

public class StravaSubscriptionInfo
{
    public long Id { get; set; }
    public string CallbackUrl { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
