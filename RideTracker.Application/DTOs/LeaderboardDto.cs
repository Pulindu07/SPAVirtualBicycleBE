namespace RideTracker.Application.DTOs;

public class LeaderboardEntryDto
{
    public int Rank { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public double DistanceCoveredKm { get; set; }
    public double ProgressPercentage { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public bool IsCurrentUser { get; set; }
    public double? CurrentPositionLat { get; set; }
    public double? CurrentPositionLng { get; set; }
}

public class LeaderboardDto
{
    public int ChallengeId { get; set; }
    public string ChallengeName { get; set; } = string.Empty;
    public double TargetDistanceKm { get; set; }
    public string ChallengeType { get; set; } = string.Empty;
    public List<LeaderboardEntryDto> Entries { get; set; } = new();
}

