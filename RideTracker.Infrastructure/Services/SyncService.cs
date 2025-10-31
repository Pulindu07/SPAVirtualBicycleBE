using Microsoft.Extensions.Logging;
using RideTracker.Application.Interfaces;
using RideTracker.Domain.Entities;
using RideTracker.Infrastructure.Data;

namespace RideTracker.Infrastructure.Services;

public class SyncService : ISyncService
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Activity> _activityRepository;
    private readonly IRepository<UserProgress> _progressRepository;
    private readonly IStravaService _stravaService;
    private readonly IRouteService _routeService;
    private readonly RideTrackerDbContext _context;
    private readonly ILogger<SyncService> _logger;

    public SyncService(
        IRepository<User> userRepository,
        IRepository<Activity> activityRepository,
        IRepository<UserProgress> progressRepository,
        IStravaService stravaService,
        IRouteService routeService,
        RideTrackerDbContext context,
        ILogger<SyncService> logger)
    {
        _userRepository = userRepository;
        _activityRepository = activityRepository;
        _progressRepository = progressRepository;
        _stravaService = stravaService;
        _routeService = routeService;
        _context = context;
        _logger = logger;
    }

    public async Task SyncUserActivitiesAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found", userId);
            return;
        }

        try
        {
            // Refresh token if needed
            var tokenRefreshed = await _stravaService.RefreshTokenIfNeededAsync(user);
            if (tokenRefreshed)
            {
                await _userRepository.UpdateAsync(user);
                await _userRepository.SaveChangesAsync();
            }

            // Fetch new activities
            var newActivities = await _stravaService.GetActivitiesAfterAsync(user.AccessToken, user.LastSync);
            
            _logger.LogInformation("Found {Count} new activities for user {UserId}", newActivities.Count, userId);

            if (newActivities.Any())
            {
                // Save new activities
                var activities = newActivities.Select(a => new Activity
                {
                    Id = a.Id,
                    UserId = user.Id,
                    Name = a.Name,
                    DistanceKm = a.Distance / 1000.0, // Convert meters to km
                    MovingTimeSec = a.MovingTime,
                    StartDate = DateTime.SpecifyKind(a.StartDate, DateTimeKind.Utc),
                    AverageSpeed = a.AverageSpeed,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                await _activityRepository.AddRangeAsync(activities);
                await _activityRepository.SaveChangesAsync();

                // Update user totals
                var additionalDistance = activities.Sum(a => a.DistanceKm);
                var additionalTime = activities.Sum(a => a.MovingTimeSec);
                
                user.TotalDistanceKm += additionalDistance;
                user.TotalMovingTimeSec += additionalTime;
            }

            user.LastSync = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);
            await _userRepository.SaveChangesAsync();

            // Update progress
            await UpdateUserProgressAsync(user);

            _logger.LogInformation("Sync completed for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing activities for user {UserId}", userId);
            throw;
        }
    }

    public async Task SyncAllUsersAsync()
    {
        var users = await _userRepository.GetAllAsync();
        
        _logger.LogInformation("Starting sync for {Count} users", users.Count);

        foreach (var user in users)
        {
            try
            {
                await SyncUserActivitiesAsync(user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync user {UserId}", user.Id);
                // Continue with other users
            }
        }

        _logger.LogInformation("Completed sync for all users");
    }

    private async Task UpdateUserProgressAsync(User user)
    {
        var totalRouteLength = await _routeService.GetTotalRouteLengthKmAsync();
        var progressPercent = Math.Min(100, (user.TotalDistanceKm / totalRouteLength) * 100);
        
        var coordinate = await _routeService.GetCoordinateAtProgressAsync(progressPercent);

        var progress = await _progressRepository.FirstOrDefaultAsync(p => p.UserId == user.Id);
        
        if (progress == null)
        {
            progress = new UserProgress
            {
                UserId = user.Id,
                TotalDistanceKm = user.TotalDistanceKm,
                ProgressPercent = progressPercent,
                CurrentLat = coordinate.Latitude,
                CurrentLng = coordinate.Longitude,
                UpdatedAt = DateTime.UtcNow
            };
            await _progressRepository.AddAsync(progress);
        }
        else
        {
            progress.TotalDistanceKm = user.TotalDistanceKm;
            progress.ProgressPercent = progressPercent;
            progress.CurrentLat = coordinate.Latitude;
            progress.CurrentLng = coordinate.Longitude;
            progress.UpdatedAt = DateTime.UtcNow;
            await _progressRepository.UpdateAsync(progress);
        }

        await _progressRepository.SaveChangesAsync();
    }
}

