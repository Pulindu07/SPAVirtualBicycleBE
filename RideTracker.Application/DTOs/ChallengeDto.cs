namespace RideTracker.Application.DTOs;

public class ChallengeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double TargetDistanceKm { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string ChallengeType { get; set; } = string.Empty;
    public int? RouteId { get; set; }
    public List<ChallengeGroupDto> ParticipatingGroups { get; set; } = new();
    public int CreatedByUserId { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int ParticipantCount { get; set; }
    public int GroupCount { get; set; } // Number of participating groups (for inter-group challenges)
    public double TotalDistanceCovered { get; set; }
    public double ProgressPercentage { get; set; }
    public string Status { get; set; } = string.Empty; // "upcoming", "in_progress", "completed"
    public int DaysRemaining { get; set; }
}

public class CreateChallengeDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double TargetDistanceKm { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string ChallengeType { get; set; } = "individual"; // "individual", "group", or "inter-group"
    public List<int> GroupIds { get; set; } = new(); // For group and inter-group challenges
    public int? RouteId { get; set; } // Optional: route to use for this challenge
}

public class UpdateChallengeDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

