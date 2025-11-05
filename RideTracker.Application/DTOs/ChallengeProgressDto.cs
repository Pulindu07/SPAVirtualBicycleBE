namespace RideTracker.Application.DTOs;

public class ChallengeProgressDto
{
    public int ChallengeId { get; set; }
    public string ChallengeName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public double DistanceCoveredKm { get; set; }
    public double ProgressPercentage { get; set; }
    public double? CurrentPositionLat { get; set; }
    public double? CurrentPositionLng { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Rank { get; set; }
}

public class GroupChallengeProgressDto
{
    public int ChallengeId { get; set; }
    public string ChallengeName { get; set; } = string.Empty;
    public double TargetDistanceKm { get; set; }
    public double TotalDistanceCovered { get; set; }
    public double ProgressPercentage { get; set; }
    public double? CurrentPositionLat { get; set; }
    public double? CurrentPositionLng { get; set; }
    public List<ChallengeProgressDto> MemberProgress { get; set; } = new();
    public DateTime? LastActivityDate { get; set; }
    public DateTime UpdatedAt { get; set; }
}

