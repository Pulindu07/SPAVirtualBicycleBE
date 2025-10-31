namespace RideTracker.Domain.Entities;

public class UserProgress
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public double TotalDistanceKm { get; set; }
    public double ProgressPercent { get; set; }
    public double CurrentLat { get; set; }
    public double CurrentLng { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
}

