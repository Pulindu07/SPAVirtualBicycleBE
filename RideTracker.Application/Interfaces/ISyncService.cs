namespace RideTracker.Application.Interfaces;

public interface ISyncService
{
    Task SyncUserActivitiesAsync(int userId);
    Task SyncAllUsersAsync();
}

