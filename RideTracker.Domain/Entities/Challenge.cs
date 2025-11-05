namespace RideTracker.Domain.Entities;

public class Challenge
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double TargetDistanceKm { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string ChallengeType { get; set; } = "individual"; // "individual", "group", or "inter-group"
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public User CreatedBy { get; set; } = null!;
    public ICollection<ChallengeGroup> ParticipatingGroups { get; set; } = new List<ChallengeGroup>();
    public ICollection<ChallengeParticipant> Participants { get; set; } = new List<ChallengeParticipant>();
    public ICollection<ChallengeProgress> ProgressRecords { get; set; } = new List<ChallengeProgress>();
}

