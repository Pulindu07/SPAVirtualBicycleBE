using RideTracker.Application.DTOs;

namespace RideTracker.Application.Interfaces;

public interface IChallengeService
{
    // Challenge Management
    Task<ChallengeDto?> GetChallengeByIdAsync(int challengeId, int? userId = null);
    Task<List<ChallengeDto>> GetUserChallengesAsync(int userId);
    Task<List<ChallengeDto>> GetGroupChallengesAsync(int groupId);
    Task<ChallengeDto> CreateChallengeAsync(int creatorUserId, CreateChallengeDto dto);
    Task<ChallengeDto?> UpdateChallengeAsync(int challengeId, int userId, UpdateChallengeDto dto);
    Task<bool> DeleteChallengeAsync(int challengeId, int userId);
    
    // Challenge Participation
    Task<bool> JoinChallengeAsync(int challengeId, int userId);
    Task<bool> LeaveChallengeAsync(int challengeId, int userId);
    Task<List<int>> GetChallengeParticipantIdsAsync(int challengeId);
    
    // Challenge Progress
    Task<ChallengeProgressDto?> GetUserChallengeProgressAsync(int challengeId, int userId);
    Task<GroupChallengeProgressDto?> GetGroupChallengeProgressAsync(int challengeId, int? userId = null);
    Task UpdateChallengeProgressAsync(int challengeId, int userId);
    
    // Leaderboard
    Task<LeaderboardDto> GetChallengeLeaderboardAsync(int challengeId, int? currentUserId = null);
    Task<InterGroupLeaderboardDto> GetInterGroupLeaderboardAsync(int challengeId, int? userId = null);
}

