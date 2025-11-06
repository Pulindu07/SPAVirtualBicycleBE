namespace RideTracker.Domain.Entities;

public class ChallengeGroup
{
    public int Id { get; set; }
    public int ChallengeId { get; set; }
    public int GroupId { get; set; }
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Challenge Challenge { get; set; } = null!;
    public Group Group { get; set; } = null!;
}

