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
    public double? CurrentPositionLat { get; set; } // For map display
    public double? CurrentPositionLng { get; set; } // For map display
}

public class InterGroupLeaderboardDto
{
    public int ChallengeId { get; set; }
    public string ChallengeName { get; set; } = string.Empty;
    public double TargetDistanceKm { get; set; }
    public List<ChallengeGroupDto> GroupRankings { get; set; } = new();
    public List<LeaderboardEntryDto>? UserGroupMemberRankings { get; set; } // Within user's group
    public int? UserGroupId { get; set; }
    public string? UserGroupName { get; set; }
}

