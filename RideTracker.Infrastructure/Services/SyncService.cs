using Microsoft.EntityFrameworkCore;
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
    private readonly IChallengeService _challengeService;
    private readonly RideTrackerDbContext _context;
    private readonly ILogger<SyncService> _logger;

    public SyncService(
        IRepository<User> userRepository,
        IRepository<Activity> activityRepository,
        IRepository<UserProgress> progressRepository,
        IStravaService stravaService,
        IRouteService routeService,
        IChallengeService challengeService,
        RideTrackerDbContext context,
        ILogger<SyncService> logger)
    {
        _userRepository = userRepository;
        _activityRepository = activityRepository;
        _progressRepository = progressRepository;
        _stravaService = stravaService;
        _routeService = routeService;
        _challengeService = challengeService;
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
            try
            {
                var tokenRefreshed = await _stravaService.RefreshTokenIfNeededAsync(user);
                if (tokenRefreshed)
                {
                    await _userRepository.UpdateAsync(user);
                    await _userRepository.SaveChangesAsync();
                }
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("refresh_token") || ex.Message.Contains("invalid"))
            {
                _logger.LogWarning(
                    "User {UserId} has invalid refresh token. User needs to re-authenticate with Strava. Error: {Error}",
                    userId, ex.Message);
                // Don't throw - skip this user but continue with others
                return;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("refresh token"))
            {
                _logger.LogWarning(
                    "User {UserId} has no refresh token. User needs to re-authenticate with Strava. Error: {Error}",
                    userId, ex.Message);
                // Don't throw - skip this user but continue with others
                return;
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
        // Calculate position directly from user's total distance traveled
        var coordinate = await _routeService.GetCoordinateAtDistanceAsync(user.TotalDistanceKm);
        
        // Also calculate progress percentage for display purposes
        var totalRouteLength = await _routeService.GetTotalRouteLengthKmAsync();
        var progressPercent = Math.Min(100, (user.TotalDistanceKm / totalRouteLength) * 100);

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

    public async Task SyncGroupChallengeAsync(int challengeId)
    {
        var challenge = await _context.Challenges
            .Include(c => c.ParticipatingGroups)
                .ThenInclude(cg => cg.Group)
                    .ThenInclude(g => g.Members.Where(m => m.IsActive))
            .FirstOrDefaultAsync(c => c.Id == challengeId && c.IsActive);

        if (challenge == null || challenge.ChallengeType != "group")
        {
            _logger.LogWarning("Group challenge {ChallengeId} not found or invalid type", challengeId);
            return;
        }

        _logger.LogInformation("Starting sync for group challenge {ChallengeId}", challengeId);

        // Get all member IDs from participating groups
        var memberIds = challenge.ParticipatingGroups
            .SelectMany(cg => cg.Group.Members.Where(m => m.IsActive))
            .Select(m => m.UserId)
            .Distinct()
            .ToList();

        foreach (var memberId in memberIds)
        {
            try
            {
                // Sync user activities
                await SyncUserActivitiesAsync(memberId);
                
                // Update challenge progress for this user
                await _challengeService.UpdateChallengeProgressAsync(challengeId, memberId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync user {UserId} for group challenge {ChallengeId}", memberId, challengeId);
                // Continue with other users
            }
        }

        _logger.LogInformation("Completed sync for group challenge {ChallengeId}", challengeId);
    }

    public async Task SyncInterGroupChallengeAsync(int challengeId)
    {
        var challenge = await _context.Challenges
            .Include(c => c.ParticipatingGroups)
                .ThenInclude(cg => cg.Group)
                    .ThenInclude(g => g.Members.Where(m => m.IsActive))
            .FirstOrDefaultAsync(c => c.Id == challengeId && c.IsActive);

        if (challenge == null || challenge.ChallengeType != "inter-group")
        {
            _logger.LogWarning("Inter-group challenge {ChallengeId} not found or invalid type", challengeId);
            return;
        }

        _logger.LogInformation("Starting sync for inter-group challenge {ChallengeId}", challengeId);

        // Get all member IDs from all participating groups
        var memberIds = challenge.ParticipatingGroups
            .SelectMany(cg => cg.Group.Members.Where(m => m.IsActive))
            .Select(m => m.UserId)
            .Distinct()
            .ToList();

        foreach (var memberId in memberIds)
        {
            try
            {
                // Sync user activities
                await SyncUserActivitiesAsync(memberId);
                
                // Update challenge progress for this user
                await _challengeService.UpdateChallengeProgressAsync(challengeId, memberId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync user {UserId} for inter-group challenge {ChallengeId}", memberId, challengeId);
                // Continue with other users
            }
        }

        _logger.LogInformation("Completed sync for inter-group challenge {ChallengeId}", challengeId);
    }

    public async Task SyncAllGroupChallengesAsync()
    {
        var groupChallenges = await _context.Challenges
            .Where(c => c.ChallengeType == "group" && c.IsActive)
            .Select(c => c.Id)
            .ToListAsync();

        _logger.LogInformation("Starting sync for {Count} group challenges", groupChallenges.Count);

        foreach (var challengeId in groupChallenges)
        {
            try
            {
                await SyncGroupChallengeAsync(challengeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync group challenge {ChallengeId}", challengeId);
                // Continue with other challenges
            }
        }

        _logger.LogInformation("Completed sync for all group challenges");
    }

    public async Task SyncAllInterGroupChallengesAsync()
    {
        var interGroupChallenges = await _context.Challenges
            .Where(c => c.ChallengeType == "inter-group" && c.IsActive)
            .Select(c => c.Id)
            .ToListAsync();

        _logger.LogInformation("Starting sync for {Count} inter-group challenges", interGroupChallenges.Count);

        foreach (var challengeId in interGroupChallenges)
        {
            try
            {
                await SyncInterGroupChallengeAsync(challengeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync inter-group challenge {ChallengeId}", challengeId);
                // Continue with other challenges
            }
        }

        _logger.LogInformation("Completed sync for all inter-group challenges");
    }
}

