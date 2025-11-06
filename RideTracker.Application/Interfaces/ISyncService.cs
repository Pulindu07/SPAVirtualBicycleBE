namespace RideTracker.Application.Interfaces;

public interface ISyncService
{
    Task SyncUserActivitiesAsync(int userId);
    Task SyncAllUsersAsync();
    Task SyncGroupChallengeAsync(int challengeId);
    Task SyncInterGroupChallengeAsync(int challengeId);
    Task SyncAllGroupChallengesAsync();
    Task SyncAllInterGroupChallengesAsync();
}

