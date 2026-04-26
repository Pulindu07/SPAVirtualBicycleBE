using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RideTracker.Application.DTOs;
using RideTracker.Application.Interfaces;
using RideTracker.Domain.Entities;
using RideTracker.Infrastructure.Data;

namespace RideTracker.Infrastructure.Services;

public class StravaWebhookProcessor : IStravaWebhookProcessor
{
    private readonly RideTrackerDbContext _context;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Activity> _activityRepository;
    private readonly IRepository<UserProgress> _progressRepository;
    private readonly IStravaService _stravaService;
    private readonly IRouteService _routeService;
    private readonly IChallengeService _challengeService;
    private readonly ILogger<StravaWebhookProcessor> _logger;

    public StravaWebhookProcessor(
        RideTrackerDbContext context,
        IRepository<User> userRepository,
        IRepository<Activity> activityRepository,
        IRepository<UserProgress> progressRepository,
        IStravaService stravaService,
        IRouteService routeService,
        IChallengeService challengeService,
        ILogger<StravaWebhookProcessor> logger)
    {
        _context = context;
        _userRepository = userRepository;
        _activityRepository = activityRepository;
        _progressRepository = progressRepository;
        _stravaService = stravaService;
        _routeService = routeService;
        _challengeService = challengeService;
        _logger = logger;
    }

    public async Task ProcessEventAsync(StravaWebhookEventDto evt)
    {
        // Idempotency: skip if we have already processed this exact event
        var existing = await _context.StravaWebhookEvents.FirstOrDefaultAsync(e =>
            e.ObjectType == evt.ObjectType
            && e.ObjectId == evt.ObjectId
            && e.AspectType == evt.AspectType
            && e.EventTime == DateTimeOffset.FromUnixTimeSeconds(evt.EventTime).UtcDateTime
            && e.Status == "processed");

        if (existing != null)
        {
            _logger.LogInformation(
                "Skipping already-processed webhook event for {ObjectType} {ObjectId} {AspectType}",
                evt.ObjectType, evt.ObjectId, evt.AspectType);
            return;
        }

        var record = new StravaWebhookEvent
        {
            SubscriptionId = evt.SubscriptionId,
            ObjectType = evt.ObjectType,
            ObjectId = evt.ObjectId,
            AspectType = evt.AspectType,
            OwnerId = evt.OwnerId,
            EventTime = DateTimeOffset.FromUnixTimeSeconds(evt.EventTime).UtcDateTime,
            ReceivedAt = DateTime.UtcNow,
            Status = "pending",
            PayloadJson = JsonSerializer.Serialize(evt)
        };
        _context.StravaWebhookEvents.Add(record);
        await _context.SaveChangesAsync();

        try
        {
            if (evt.ObjectType.Equals("activity", StringComparison.OrdinalIgnoreCase))
            {
                await HandleActivityEventAsync(evt);
            }
            else if (evt.ObjectType.Equals("athlete", StringComparison.OrdinalIgnoreCase))
            {
                await HandleAthleteEventAsync(evt);
            }
            else
            {
                _logger.LogWarning("Unknown webhook object_type {ObjectType}", evt.ObjectType);
            }

            record.Status = "processed";
            record.ProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            record.Status = "failed";
            record.ErrorMessage = ex.Message;
            record.ProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogError(ex,
                "Failed to process webhook event {ObjectType} {ObjectId} {AspectType}",
                evt.ObjectType, evt.ObjectId, evt.AspectType);
            throw;
        }
    }

    private async Task HandleActivityEventAsync(StravaWebhookEventDto evt)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.StravaId == evt.OwnerId);
        if (user == null)
        {
            _logger.LogWarning("Webhook for unknown athlete {OwnerId}; ignoring", evt.OwnerId);
            return;
        }

        if (!user.IsActive)
        {
            _logger.LogInformation("Skipping webhook for inactive user {UserId}", user.Id);
            return;
        }

        if (evt.AspectType.Equals("delete", StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _context.Activities.FirstOrDefaultAsync(a => a.Id == evt.ObjectId && a.UserId == user.Id);
            if (existing != null)
            {
                _context.Activities.Remove(existing);
                await _context.SaveChangesAsync();
            }

            await RecomputeUserTotalsAsync(user);
            await UpdateUserProgressAsync(user);
            await UpdateChallengeProgressForUserAsync(user.Id);
            return;
        }

        if (!evt.AspectType.Equals("create", StringComparison.OrdinalIgnoreCase)
            && !evt.AspectType.Equals("update", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Unhandled activity aspect_type {AspectType}", evt.AspectType);
            return;
        }

        try
        {
            var refreshed = await _stravaService.RefreshTokenIfNeededAsync(user);
            if (refreshed)
            {
                await _userRepository.UpdateAsync(user);
                await _userRepository.SaveChangesAsync();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is InvalidOperationException)
        {
            _logger.LogWarning(ex,
                "Cannot refresh token for user {UserId}; needs re-auth. Skipping webhook event.", user.Id);
            return;
        }

        var dto = await _stravaService.GetActivityByIdAsync(user.AccessToken, evt.ObjectId);
        if (dto == null)
        {
            // Activity was filtered out (not a Ride, manual, distance=0) or deleted before fetch.
            // If we have it locally, remove it so totals stay consistent.
            var stale = await _context.Activities.FirstOrDefaultAsync(a => a.Id == evt.ObjectId && a.UserId == user.Id);
            if (stale != null)
            {
                _context.Activities.Remove(stale);
                await _context.SaveChangesAsync();
                await RecomputeUserTotalsAsync(user);
                await UpdateUserProgressAsync(user);
                await UpdateChallengeProgressForUserAsync(user.Id);
            }
            return;
        }

        var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == dto.Id);
        if (activity == null)
        {
            activity = new Activity
            {
                Id = dto.Id,
                UserId = user.Id,
                Name = dto.Name,
                DistanceKm = dto.Distance / 1000.0,
                MovingTimeSec = dto.MovingTime,
                StartDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc),
                AverageSpeed = dto.AverageSpeed,
                CreatedAt = DateTime.UtcNow
            };
            _context.Activities.Add(activity);
        }
        else
        {
            activity.Name = dto.Name;
            activity.DistanceKm = dto.Distance / 1000.0;
            activity.MovingTimeSec = dto.MovingTime;
            activity.StartDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc);
            activity.AverageSpeed = dto.AverageSpeed;
        }
        await _context.SaveChangesAsync();

        await RecomputeUserTotalsAsync(user);
        await UpdateUserProgressAsync(user);
        await UpdateChallengeProgressForUserAsync(user.Id);
    }

    private async Task HandleAthleteEventAsync(StravaWebhookEventDto evt)
    {
        // Strava only emits one athlete event today: deauthorize (updates.authorized = "false").
        if (evt.Updates == null
            || !evt.Updates.TryGetValue("authorized", out var authorized)
            || !string.Equals(authorized, "false", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Athlete event without deauthorize signal; ignoring. updates={Updates}",
                evt.Updates == null ? "null" : JsonSerializer.Serialize(evt.Updates));
            return;
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.StravaId == evt.OwnerId);
        if (user == null)
        {
            _logger.LogWarning("Deauthorize webhook for unknown athlete {OwnerId}", evt.OwnerId);
            return;
        }

        user.IsActive = false;
        user.AccessToken = string.Empty;
        user.RefreshToken = string.Empty;
        user.TokenExpiry = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogWarning("User {UserId} (Strava {StravaId}) deauthorized the app; marked inactive.",
            user.Id, user.StravaId);
    }

    private async Task RecomputeUserTotalsAsync(User user)
    {
        var totals = await _context.Activities
            .Where(a => a.UserId == user.Id)
            .GroupBy(a => a.UserId)
            .Select(g => new
            {
                Distance = g.Sum(a => a.DistanceKm),
                Time = g.Sum(a => a.MovingTimeSec)
            })
            .FirstOrDefaultAsync();

        user.TotalDistanceKm = totals?.Distance ?? 0;
        user.TotalMovingTimeSec = totals?.Time ?? 0;
        user.LastSync = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    private async Task UpdateUserProgressAsync(User user)
    {
        var coordinate = await _routeService.GetCoordinateAtDistanceAsync(user.TotalDistanceKm);
        var totalRouteLength = await _routeService.GetTotalRouteLengthKmAsync();
        var progressPercent = totalRouteLength > 0
            ? Math.Min(100, (user.TotalDistanceKm / totalRouteLength) * 100)
            : 0;

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

    private async Task UpdateChallengeProgressForUserAsync(int userId)
    {
        var now = DateTime.UtcNow;

        var directChallengeIds = await _context.ChallengeParticipants
            .Where(p => p.UserId == userId && p.IsActive
                        && p.Challenge.IsActive
                        && p.Challenge.StartDate <= now && p.Challenge.EndDate >= now)
            .Select(p => p.ChallengeId)
            .ToListAsync();

        var groupChallengeIds = await _context.GroupMembers
            .Where(gm => gm.UserId == userId && gm.IsActive)
            .SelectMany(gm => gm.Group.ChallengeParticipations
                .Where(cg => cg.IsActive
                             && cg.Challenge.IsActive
                             && cg.Challenge.StartDate <= now
                             && cg.Challenge.EndDate >= now)
                .Select(cg => cg.ChallengeId))
            .Distinct()
            .ToListAsync();

        var allChallengeIds = directChallengeIds.Concat(groupChallengeIds).Distinct().ToList();

        foreach (var challengeId in allChallengeIds)
        {
            try
            {
                await _challengeService.UpdateChallengeProgressAsync(challengeId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update challenge {ChallengeId} progress for user {UserId}",
                    challengeId, userId);
            }
        }
    }
}
