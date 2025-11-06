namespace RideTracker.Domain.Entities;

public class ChallengeProgress
{
    public int Id { get; set; }
    public int ChallengeId { get; set; }
    public int UserId { get; set; } // Individual user's progress
    public double DistanceCoveredKm { get; set; }
    public double ProgressPercentage { get; set; }
    public double? CurrentPositionLat { get; set; }
    public double? CurrentPositionLng { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Challenge Challenge { get; set; } = null!;
    public User User { get; set; } = null!;
}

