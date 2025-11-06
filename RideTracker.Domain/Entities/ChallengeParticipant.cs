namespace RideTracker.Domain.Entities;

public class ChallengeParticipant
{
    public int Id { get; set; }
    public int ChallengeId { get; set; }
    public int UserId { get; set; }
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Challenge Challenge { get; set; } = null!;
    public User User { get; set; } = null!;
}

