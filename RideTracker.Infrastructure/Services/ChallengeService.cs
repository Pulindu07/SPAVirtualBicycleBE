using Microsoft.EntityFrameworkCore;
using RideTracker.Application.DTOs;
using RideTracker.Application.Interfaces;
using RideTracker.Domain.Entities;
using RideTracker.Infrastructure.Data;

namespace RideTracker.Infrastructure.Services;

public class ChallengeService : IChallengeService
{
    private readonly RideTrackerDbContext _context;

    public ChallengeService(RideTrackerDbContext context)
    {
        _context = context;
    }

    private async Task<double> GetRouteTotalDistanceAsync(int? routeId)
    {
        if (!routeId.HasValue)
        {
            // If no route specified, get the default route (first active route)
            var defaultRoute = await _context.Routes
                .Where(r => r.IsActive)
                .OrderBy(r => r.Id)
                .FirstOrDefaultAsync();
            
            if (defaultRoute != null)
                return defaultRoute.TotalDistanceKm;
            
            // Fallback to hardcoded value if no routes exist
            return 572.0;
        }

        var route = await _context.Routes.FindAsync(routeId.Value);
        return route?.TotalDistanceKm ?? 572.0;
    }

    private async Task<RoutePoint?> GetRoutePointAtDistanceAsync(int? routeId, double distanceKm)
    {
        if (!routeId.HasValue)
        {
            // If no route specified, get the default route
            var defaultRoute = await _context.Routes
                .Where(r => r.IsActive)
                .OrderBy(r => r.Id)
                .FirstOrDefaultAsync();
            
            if (defaultRoute == null)
                return null;
            
            routeId = defaultRoute.Id;
        }

        var route = await _context.Routes
            .Include(r => r.RoutePoints.OrderBy(rp => rp.OrderIndex))
            .FirstOrDefaultAsync(r => r.Id == routeId.Value);

        if (route == null || !route.RoutePoints.Any())
            return null;

        var totalDistance = route.TotalDistanceKm;
        if (totalDistance <= 0)
            return null;

        // Calculate which route point corresponds to the distance
        // We need to find the point where cumulative distance >= distanceKm
        var orderedPoints = route.RoutePoints.OrderBy(rp => rp.OrderIndex).ToList();
        double cumulativeDistance = 0;

        for (int i = 0; i < orderedPoints.Count - 1; i++)
        {
            var point1 = orderedPoints[i];
            var point2 = orderedPoints[i + 1];
            
            var segmentDistance = CalculateDistance(
                point1.Latitude, point1.Longitude,
                point2.Latitude, point2.Longitude
            );

            if (cumulativeDistance + segmentDistance >= distanceKm)
            {
                // Interpolate between point1 and point2
                var localProgress = (distanceKm - cumulativeDistance) / segmentDistance;
                var lat = point1.Latitude + (point2.Latitude - point1.Latitude) * localProgress;
                var lng = point1.Longitude + (point2.Longitude - point1.Longitude) * localProgress;
                
                // Return the closest actual route point (point2)
                return point2;
            }

            cumulativeDistance += segmentDistance;
        }

        // If distance exceeds route, return last point
        return orderedPoints.LastOrDefault();
    }

    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Earth's radius in kilometers
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    private string GetChallengeStatus(DateTime startDate, DateTime endDate)
    {
        var now = DateTime.UtcNow;
        if (now < startDate) return "upcoming";
        if (now > endDate) return "completed";
        return "in_progress";
    }

    private int GetDaysRemaining(DateTime endDate)
    {
        var remaining = (endDate - DateTime.UtcNow).Days;
        return remaining > 0 ? remaining : 0;
    }

    private async Task<List<ChallengeGroupDto>> GetChallengeGroupsWithProgressAsync(int challengeId)
    {
        var challengeGroups = await _context.ChallengeGroups
            .Include(cg => cg.Group)
                .ThenInclude(g => g.Members.Where(m => m.IsActive))
            .Where(cg => cg.ChallengeId == challengeId && cg.IsActive)
            .ToListAsync();

        var result = new List<ChallengeGroupDto>();
        int rank = 1;

        foreach (var cg in challengeGroups.OrderByDescending(cg => 0)) // Will calculate distance below
        {
            // Get all member IDs in this group
            var memberIds = cg.Group.Members.Where(m => m.IsActive).Select(m => m.UserId).ToList();

            // Get total distance for all group members in this challenge
            var totalDistance = await _context.ChallengeProgress
                .Where(cp => cp.ChallengeId == challengeId && memberIds.Contains(cp.UserId))
                .SumAsync(cp => cp.DistanceCoveredKm);

            var challenge = await _context.Challenges.FindAsync(challengeId);
            var progressPercentage = challenge!.TargetDistanceKm > 0
                ? (totalDistance / challenge.TargetDistanceKm) * 100
                : 0;

            result.Add(new ChallengeGroupDto
            {
                GroupId = cg.GroupId,
                GroupName = cg.Group.Name,
                GroupIconUrl = cg.Group.IconUrl,
                JoinedAt = cg.JoinedAt,
                TotalDistanceCovered = totalDistance,
                ProgressPercentage = progressPercentage,
                MemberCount = cg.Group.Members.Count(m => m.IsActive),
                Rank = 0 // Will set after sorting
            });
        }

        // Sort by distance and assign ranks
        result = result.OrderByDescending(g => g.TotalDistanceCovered).ToList();
        for (int i = 0; i < result.Count; i++)
        {
            result[i].Rank = i + 1;
        }

        return result;
    }

    public async Task<ChallengeDto?> GetChallengeByIdAsync(int challengeId, int? userId = null)
    {
        var challenge = await _context.Challenges
            .Include(c => c.ParticipatingGroups)
                .ThenInclude(cg => cg.Group)
            .Include(c => c.CreatedBy)
            .Include(c => c.Participants)
            .Include(c => c.ProgressRecords)
            .Where(c => c.Id == challengeId && c.IsActive)
            .FirstOrDefaultAsync();

        if (challenge == null) return null;

        double totalDistance;
        double progressPercentage;
        
        // For group challenges: use logged-in user's distance
        // For inter-group challenges: use sum of all members
        if (challenge.ChallengeType == "group" && userId.HasValue)
        {
            // Get the logged-in user's progress
            var userProgress = challenge.ProgressRecords.FirstOrDefault(p => p.UserId == userId.Value);
            totalDistance = userProgress?.DistanceCoveredKm ?? 0;
            progressPercentage = challenge.TargetDistanceKm > 0
                ? (totalDistance / challenge.TargetDistanceKm) * 100
                : 0;
        }
        else
        {
            // Individual or inter-group: sum of all members
            totalDistance = challenge.ProgressRecords
                .Where(p => p.ChallengeId == challengeId)
                .Sum(p => p.DistanceCoveredKm);
            progressPercentage = challenge.TargetDistanceKm > 0
                ? (totalDistance / challenge.TargetDistanceKm) * 100
                : 0;
        }

        var participatingGroups = await GetChallengeGroupsWithProgressAsync(challengeId);

        return new ChallengeDto
        {
            Id = challenge.Id,
            Name = challenge.Name,
            Description = challenge.Description,
            TargetDistanceKm = challenge.TargetDistanceKm,
            StartDate = challenge.StartDate,
            EndDate = challenge.EndDate,
            ChallengeType = challenge.ChallengeType,
            RouteId = challenge.RouteId,
            ParticipatingGroups = participatingGroups,
            CreatedByUserId = challenge.CreatedByUserId,
            CreatedByUsername = challenge.CreatedBy.Username,
            CreatedAt = challenge.CreatedAt,
            ParticipantCount = challenge.Participants.Count(p => p.IsActive),
            TotalDistanceCovered = totalDistance,
            ProgressPercentage = progressPercentage,
            Status = GetChallengeStatus(challenge.StartDate, challenge.EndDate),
            DaysRemaining = GetDaysRemaining(challenge.EndDate)
        };
    }

    public async Task<List<ChallengeDto>> GetUserChallengesAsync(int userId)
    {
        var challengeIds = await _context.ChallengeParticipants
            .Where(cp => cp.UserId == userId && cp.IsActive)
            .Select(cp => cp.ChallengeId)
            .ToListAsync();

        var challenges = await _context.Challenges
            .Include(c => c.ParticipatingGroups)
                .ThenInclude(cg => cg.Group)
            .Include(c => c.CreatedBy)
            .Include(c => c.Participants)
            .Include(c => c.ProgressRecords)
            .Where(c => challengeIds.Contains(c.Id) && c.IsActive)
            .ToListAsync();

        var result = new List<ChallengeDto>();

        foreach (var challenge in challenges)
        {
            double totalDistance;
            double progressPercentage;
            
            // For group challenges: use logged-in user's distance
            // For inter-group challenges: use sum of all members
            if (challenge.ChallengeType == "group")
            {
                // Get the logged-in user's progress
                var userProgress = challenge.ProgressRecords.FirstOrDefault(p => p.UserId == userId);
                totalDistance = userProgress?.DistanceCoveredKm ?? 0;
                progressPercentage = challenge.TargetDistanceKm > 0
                    ? (totalDistance / challenge.TargetDistanceKm) * 100
                    : 0;
            }
            else
            {
                // Individual or inter-group: sum of all members
                totalDistance = challenge.ProgressRecords.Sum(p => p.DistanceCoveredKm);
                progressPercentage = challenge.TargetDistanceKm > 0
                    ? (totalDistance / challenge.TargetDistanceKm) * 100
                    : 0;
            }

            var participatingGroups = await GetChallengeGroupsWithProgressAsync(challenge.Id);

            result.Add(new ChallengeDto
            {
                Id = challenge.Id,
                Name = challenge.Name,
                Description = challenge.Description,
                TargetDistanceKm = challenge.TargetDistanceKm,
                StartDate = challenge.StartDate,
                EndDate = challenge.EndDate,
                ChallengeType = challenge.ChallengeType,
                RouteId = challenge.RouteId,
                ParticipatingGroups = participatingGroups,
                CreatedByUserId = challenge.CreatedByUserId,
                CreatedByUsername = challenge.CreatedBy.Username,
                CreatedAt = challenge.CreatedAt,
                ParticipantCount = challenge.Participants.Count(p => p.IsActive),
                TotalDistanceCovered = totalDistance,
                ProgressPercentage = progressPercentage,
                Status = GetChallengeStatus(challenge.StartDate, challenge.EndDate),
                DaysRemaining = GetDaysRemaining(challenge.EndDate)
            });
        }

        return result.OrderByDescending(c => c.CreatedAt).ToList();
    }

    public async Task<List<ChallengeDto>> GetGroupChallengesAsync(int groupId)
    {
        // Get challenges where this group is participating
        var challengeIds = await _context.ChallengeGroups
            .Where(cg => cg.GroupId == groupId && cg.IsActive)
            .Select(cg => cg.ChallengeId)
            .ToListAsync();

        var challenges = await _context.Challenges
            .Include(c => c.ParticipatingGroups)
                .ThenInclude(cg => cg.Group)
            .Include(c => c.CreatedBy)
            .Include(c => c.Participants)
            .Include(c => c.ProgressRecords)
            .Where(c => challengeIds.Contains(c.Id) && c.IsActive)
            .ToListAsync();

        var result = new List<ChallengeDto>();

        foreach (var challenge in challenges)
        {
            var totalDistance = challenge.ProgressRecords.Sum(p => p.DistanceCoveredKm);
            var progressPercentage = challenge.TargetDistanceKm > 0
                ? (totalDistance / challenge.TargetDistanceKm) * 100
                : 0;

            var participatingGroups = await GetChallengeGroupsWithProgressAsync(challenge.Id);

            result.Add(new ChallengeDto
            {
                Id = challenge.Id,
                Name = challenge.Name,
                Description = challenge.Description,
                TargetDistanceKm = challenge.TargetDistanceKm,
                StartDate = challenge.StartDate,
                EndDate = challenge.EndDate,
                ChallengeType = challenge.ChallengeType,
                RouteId = challenge.RouteId,
                ParticipatingGroups = participatingGroups,
                CreatedByUserId = challenge.CreatedByUserId,
                CreatedByUsername = challenge.CreatedBy.Username,
                CreatedAt = challenge.CreatedAt,
                ParticipantCount = challenge.Participants.Count(p => p.IsActive),
                TotalDistanceCovered = totalDistance,
                ProgressPercentage = progressPercentage,
                Status = GetChallengeStatus(challenge.StartDate, challenge.EndDate),
                DaysRemaining = GetDaysRemaining(challenge.EndDate)
            });
        }

        return result.OrderByDescending(c => c.CreatedAt).ToList();
    }

    public async Task<ChallengeDto> CreateChallengeAsync(int creatorUserId, CreateChallengeDto dto)
    {
        // Only super admins can create challenges
        var creator = await _context.Users.FindAsync(creatorUserId);
        if (creator == null || !creator.IsSuperAdmin)
            throw new UnauthorizedAccessException("Only super admins can create challenges");

        // Validate groups if it's a group or inter-group challenge
        if ((dto.ChallengeType == "group" || dto.ChallengeType == "inter-group") && dto.GroupIds.Count == 0)
            throw new InvalidOperationException("Group or inter-group challenges must have at least one group");

        var challenge = new Challenge
        {
            Name = dto.Name,
            Description = dto.Description,
            TargetDistanceKm = dto.TargetDistanceKm,
            StartDate = dto.StartDate.ToUniversalTime(),
            EndDate = dto.EndDate.ToUniversalTime(),
            ChallengeType = dto.ChallengeType,
            RouteId = dto.RouteId,
            CreatedByUserId = creatorUserId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Challenges.Add(challenge);
        await _context.SaveChangesAsync();

        // Add participating groups
        foreach (var groupId in dto.GroupIds)
        {
            var challengeGroup = new ChallengeGroup
            {
                ChallengeId = challenge.Id,
                GroupId = groupId,
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            };
            _context.ChallengeGroups.Add(challengeGroup);
        }

        await _context.SaveChangesAsync();

        // Auto-join creator to the challenge
        await JoinChallengeAsync(challenge.Id, creatorUserId);

        // If it's a group or inter-group challenge, auto-join all members of participating groups
        if (dto.ChallengeType == "group" || dto.ChallengeType == "inter-group")
        {
            foreach (var groupId in dto.GroupIds)
            {
                var groupMembers = await _context.GroupMembers
                    .Where(gm => gm.GroupId == groupId && gm.IsActive && gm.UserId != creatorUserId)
                    .Select(gm => gm.UserId)
                    .ToListAsync();

                foreach (var memberId in groupMembers)
                {
                    await JoinChallengeAsync(challenge.Id, memberId);
                }
            }
        }

        return (await GetChallengeByIdAsync(challenge.Id))!;
    }

    public async Task<ChallengeDto?> UpdateChallengeAsync(int challengeId, int userId, UpdateChallengeDto dto)
    {
        var challenge = await _context.Challenges.FindAsync(challengeId);
        if (challenge == null || !challenge.IsActive) return null;

        // Only creator can update
        if (challenge.CreatedByUserId != userId)
            return null;

        if (!string.IsNullOrWhiteSpace(dto.Name))
            challenge.Name = dto.Name;

        if (dto.Description != null)
            challenge.Description = dto.Description;

        if (dto.StartDate.HasValue)
            challenge.StartDate = dto.StartDate.Value.ToUniversalTime();

        if (dto.EndDate.HasValue)
            challenge.EndDate = dto.EndDate.Value.ToUniversalTime();

        await _context.SaveChangesAsync();

        return await GetChallengeByIdAsync(challengeId);
    }

    public async Task<bool> DeleteChallengeAsync(int challengeId, int userId)
    {
        var challenge = await _context.Challenges.FindAsync(challengeId);
        if (challenge == null || !challenge.IsActive) return false;

        // Only creator can delete
        if (challenge.CreatedByUserId != userId)
            return false;

        challenge.IsActive = false;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> JoinChallengeAsync(int challengeId, int userId)
    {
        var challenge = await _context.Challenges.FindAsync(challengeId);
        if (challenge == null || !challenge.IsActive) return false;

        // Check if already a participant
        var existingParticipant = await _context.ChallengeParticipants
            .FirstOrDefaultAsync(cp => cp.ChallengeId == challengeId && cp.UserId == userId);

        if (existingParticipant != null)
        {
            if (existingParticipant.IsActive) return false;

            // Reactivate
            existingParticipant.IsActive = true;
            existingParticipant.JoinedAt = DateTime.UtcNow;
        }
        else
        {
            var participant = new ChallengeParticipant
            {
                ChallengeId = challengeId,
                UserId = userId,
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            };
            _context.ChallengeParticipants.Add(participant);
        }

        // Create initial progress record
        var existingProgress = await _context.ChallengeProgress
            .FirstOrDefaultAsync(cp => cp.ChallengeId == challengeId && cp.UserId == userId);

        if (existingProgress == null)
        {
            var progress = new ChallengeProgress
            {
                ChallengeId = challengeId,
                UserId = userId,
                DistanceCoveredKm = 0,
                ProgressPercentage = 0,
                UpdatedAt = DateTime.UtcNow
            };
            _context.ChallengeProgress.Add(progress);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> LeaveChallengeAsync(int challengeId, int userId)
    {
        var participant = await _context.ChallengeParticipants
            .FirstOrDefaultAsync(cp => cp.ChallengeId == challengeId && cp.UserId == userId);

        if (participant == null || !participant.IsActive) return false;

        // Can't leave if you're the creator
        var challenge = await _context.Challenges.FindAsync(challengeId);
        if (challenge!.CreatedByUserId == userId)
            return false;

        participant.IsActive = false;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<List<int>> GetChallengeParticipantIdsAsync(int challengeId)
    {
        return await _context.ChallengeParticipants
            .Where(cp => cp.ChallengeId == challengeId && cp.IsActive)
            .Select(cp => cp.UserId)
            .ToListAsync();
    }

    public async Task<ChallengeProgressDto?> GetUserChallengeProgressAsync(int challengeId, int userId)
    {
        var progress = await _context.ChallengeProgress
            .Include(cp => cp.Challenge)
            .Include(cp => cp.User)
            .FirstOrDefaultAsync(cp => cp.ChallengeId == challengeId && cp.UserId == userId);

        if (progress == null) return null;

        // Get rank
        var allProgress = await _context.ChallengeProgress
            .Where(cp => cp.ChallengeId == challengeId)
            .OrderByDescending(cp => cp.DistanceCoveredKm)
            .ToListAsync();

        var rank = allProgress.FindIndex(p => p.UserId == userId) + 1;

        return new ChallengeProgressDto
        {
            ChallengeId = progress.ChallengeId,
            ChallengeName = progress.Challenge.Name,
            UserId = progress.UserId,
            Username = progress.User.Username,
            FirstName = progress.User.FirstName,
            LastName = progress.User.LastName,
            DistanceCoveredKm = progress.DistanceCoveredKm,
            ProgressPercentage = progress.ProgressPercentage,
            CurrentPositionLat = progress.CurrentPositionLat,
            CurrentPositionLng = progress.CurrentPositionLng,
            LastActivityDate = progress.LastActivityDate,
            UpdatedAt = progress.UpdatedAt,
            Rank = rank
        };
    }

    public async Task<GroupChallengeProgressDto?> GetGroupChallengeProgressAsync(int challengeId, int? userId = null)
    {
        var challenge = await _context.Challenges
            .Include(c => c.ProgressRecords)
                .ThenInclude(p => p.User)
            .Include(c => c.ParticipatingGroups)
            .FirstOrDefaultAsync(c => c.Id == challengeId && c.IsActive);

        if (challenge == null || (challenge.ChallengeType != "group" && challenge.ChallengeType != "inter-group")) 
            return null;

        double totalDistance;
        double progressPercentage;
        
        // For group challenges: use logged-in user's distance
        // For inter-group challenges: use sum of all members
        if (challenge.ChallengeType == "group" && userId.HasValue)
        {
            // Get the logged-in user's progress
            var userProgress = challenge.ProgressRecords.FirstOrDefault(p => p.UserId == userId.Value);
            totalDistance = userProgress?.DistanceCoveredKm ?? 0;
            progressPercentage = challenge.TargetDistanceKm > 0
                ? (totalDistance / challenge.TargetDistanceKm) * 100
                : 0;
        }
        else
        {
            // Inter-group: sum of all members
            totalDistance = challenge.ProgressRecords.Sum(p => p.DistanceCoveredKm);
            progressPercentage = challenge.TargetDistanceKm > 0
                ? (totalDistance / challenge.TargetDistanceKm) * 100
                : 0;
        }

        // Calculate group position on route
        var routePoint = await GetRoutePointAtDistanceAsync(challenge.RouteId, totalDistance);

        var memberProgress = challenge.ProgressRecords
            .OrderByDescending(p => p.DistanceCoveredKm)
            .Select((p, index) => new ChallengeProgressDto
            {
                ChallengeId = p.ChallengeId,
                ChallengeName = challenge.Name,
                UserId = p.UserId,
                Username = p.User.Username,
                FirstName = p.User.FirstName,
                LastName = p.User.LastName,
                DistanceCoveredKm = p.DistanceCoveredKm,
                ProgressPercentage = p.ProgressPercentage,
                CurrentPositionLat = p.CurrentPositionLat,
                CurrentPositionLng = p.CurrentPositionLng,
                LastActivityDate = p.LastActivityDate,
                UpdatedAt = p.UpdatedAt,
                Rank = index + 1
            })
            .ToList();

        return new GroupChallengeProgressDto
        {
            ChallengeId = challengeId,
            ChallengeName = challenge.Name,
            TargetDistanceKm = challenge.TargetDistanceKm,
            TotalDistanceCovered = totalDistance,
            ProgressPercentage = progressPercentage,
            CurrentPositionLat = routePoint?.Latitude,
            CurrentPositionLng = routePoint?.Longitude,
            MemberProgress = memberProgress,
            LastActivityDate = challenge.ProgressRecords.Max(p => p.LastActivityDate),
            UpdatedAt = DateTime.UtcNow
        };
    }

    public async Task UpdateChallengeProgressAsync(int challengeId, int userId)
    {
        var progress = await _context.ChallengeProgress
            .Include(cp => cp.Challenge)
            .FirstOrDefaultAsync(cp => cp.ChallengeId == challengeId && cp.UserId == userId);

        if (progress == null) return;

        // Get user's activities within the challenge date range
        var challenge = progress.Challenge;
        var activities = await _context.Activities
            .Where(a => a.UserId == userId && 
                       a.StartDate >= challenge.StartDate && 
                       a.StartDate <= challenge.EndDate)
            .ToListAsync();

        var totalDistance = activities.Sum(a => a.DistanceKm);
        var progressPercentage = challenge.TargetDistanceKm > 0
            ? (totalDistance / challenge.TargetDistanceKm) * 100
            : 0;

        // Calculate position on route
        var routePoint = await GetRoutePointAtDistanceAsync(challenge.RouteId, totalDistance);

        progress.DistanceCoveredKm = totalDistance;
        progress.ProgressPercentage = Math.Min(progressPercentage, 100);
        progress.CurrentPositionLat = routePoint?.Latitude;
        progress.CurrentPositionLng = routePoint?.Longitude;
        progress.LastActivityDate = activities.OrderByDescending(a => a.StartDate).FirstOrDefault()?.StartDate;
        progress.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task<LeaderboardDto> GetChallengeLeaderboardAsync(int challengeId, int? currentUserId = null)
    {
        var challenge = await _context.Challenges
            .Include(c => c.ProgressRecords)
                .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(c => c.Id == challengeId && c.IsActive);

        if (challenge == null)
            return new LeaderboardDto();

        var entries = challenge.ProgressRecords
            .OrderByDescending(p => p.DistanceCoveredKm)
            .Select((p, index) => new LeaderboardEntryDto
            {
                Rank = index + 1,
                UserId = p.UserId,
                Username = p.User.Username,
                FirstName = p.User.FirstName,
                LastName = p.User.LastName,
                DistanceCoveredKm = p.DistanceCoveredKm,
                ProgressPercentage = p.ProgressPercentage,
                LastActivityDate = p.LastActivityDate,
                IsCurrentUser = currentUserId.HasValue && p.UserId == currentUserId.Value,
                CurrentPositionLat = p.CurrentPositionLat,
                CurrentPositionLng = p.CurrentPositionLng
            })
            .ToList();

        return new LeaderboardDto
        {
            ChallengeId = challengeId,
            ChallengeName = challenge.Name,
            TargetDistanceKm = challenge.TargetDistanceKm,
            ChallengeType = challenge.ChallengeType,
            Entries = entries
        };
    }

    public async Task<InterGroupLeaderboardDto> GetInterGroupLeaderboardAsync(int challengeId)
    {
        var challenge = await _context.Challenges
            .FirstOrDefaultAsync(c => c.Id == challengeId && c.IsActive);

        if (challenge == null)
            return new InterGroupLeaderboardDto();

        var groupRankings = await GetChallengeGroupsWithProgressAsync(challengeId);

        return new InterGroupLeaderboardDto
        {
            ChallengeId = challengeId,
            ChallengeName = challenge.Name,
            TargetDistanceKm = challenge.TargetDistanceKm,
            GroupRankings = groupRankings
        };
    }
}
