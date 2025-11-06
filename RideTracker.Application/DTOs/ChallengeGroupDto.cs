namespace RideTracker.Application.DTOs;

public class ChallengeGroupDto
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string? GroupIconUrl { get; set; }
    public DateTime JoinedAt { get; set; }
    public double TotalDistanceCovered { get; set; }
    public double ProgressPercentage { get; set; }
    public int MemberCount { get; set; }
    public int Rank { get; set; }
}

public class InterGroupLeaderboardDto
{
    public int ChallengeId { get; set; }
    public string ChallengeName { get; set; } = string.Empty;
    public double TargetDistanceKm { get; set; }
    public List<ChallengeGroupDto> GroupRankings { get; set; } = new();
}

