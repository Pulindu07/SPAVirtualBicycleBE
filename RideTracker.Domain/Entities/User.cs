namespace RideTracker.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public long StravaId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime TokenExpiry { get; set; }
    public double TotalDistanceKm { get; set; }
    public long TotalMovingTimeSec { get; set; }
    public DateTime LastSync { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public UserProgress? Progress { get; set; }
}

