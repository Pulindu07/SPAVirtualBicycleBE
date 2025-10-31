namespace RideTracker.Domain.Entities;

public class Activity
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double DistanceKm { get; set; }
    public long MovingTimeSec { get; set; }
    public DateTime StartDate { get; set; }
    public double AverageSpeed { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
}

