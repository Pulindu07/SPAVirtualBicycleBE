using Microsoft.EntityFrameworkCore;
using RideTracker.Application.DTOs;
using RideTracker.Application.Interfaces;
using RideTracker.Domain.Entities;
using RideTracker.Infrastructure.Data;

namespace RideTracker.Infrastructure.Services;

public class ChallengeService : IChallengeService
{
    private readonly RideTrackerDbContext _context;
    private const double TOTAL_ROUTE_DISTANCE = 572.0; // Dondra Head to Point Pedro

    public ChallengeService(RideTrackerDbContext context)
    {
        _context = context;
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

    public async Task<ChallengeDto?> GetChallengeByIdAsync(int challengeId)
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

        var totalDistance = challenge.ProgressRecords
            .Where(p => p.ChallengeId == challengeId)
            .Sum(p => p.DistanceCoveredKm);

        var progressPercentage = challenge.TargetDistanceKm > 0
            ? (totalDistance / challenge.TargetDistanceKm) * 100
            : 0;

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
        // Validate groups if it's a group or inter-group challenge
        if ((dto.ChallengeType == "group" || dto.ChallengeType == "inter-group") && dto.GroupIds.Count == 0)
            throw new InvalidOperationException("Group or inter-group challenges must have at least one group");

        // Verify creator is a member of all specified groups
        foreach (var groupId in dto.GroupIds)
        {
            var isMember = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == creatorUserId && gm.IsActive);

            if (!isMember)
                throw new UnauthorizedAccessException($"You must be a member of group {groupId} to create a challenge for it");
        }

        var challenge = new Challenge
        {
            Name = dto.Name,
            Description = dto.Description,
            TargetDistanceKm = dto.TargetDistanceKm,
            StartDate = dto.StartDate.ToUniversalTime(),
            EndDate = dto.EndDate.ToUniversalTime(),
            ChallengeType = dto.ChallengeType,
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

    public async Task<GroupChallengeProgressDto?> GetGroupChallengeProgressAsync(int challengeId)
    {
        var challenge = await _context.Challenges
            .Include(c => c.ProgressRecords)
                .ThenInclude(p => p.User)
            .Include(c => c.ParticipatingGroups)
            .FirstOrDefaultAsync(c => c.Id == challengeId && c.IsActive);

        if (challenge == null || (challenge.ChallengeType != "group" && challenge.ChallengeType != "inter-group")) 
            return null;

        var totalDistance = challenge.ProgressRecords.Sum(p => p.DistanceCoveredKm);
        var progressPercentage = challenge.TargetDistanceKm > 0
            ? (totalDistance / challenge.TargetDistanceKm) * 100
            : 0;

        // Calculate group position on route
        var routePoint = await _context.RoutePoints
            .Where(rp => rp.OrderIndex <= totalDistance / TOTAL_ROUTE_DISTANCE * 100)
            .OrderByDescending(rp => rp.OrderIndex)
            .FirstOrDefaultAsync();

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
        var routePoint = await _context.RoutePoints
            .Where(rp => rp.OrderIndex <= totalDistance / TOTAL_ROUTE_DISTANCE * 100)
            .OrderByDescending(rp => rp.OrderIndex)
            .FirstOrDefaultAsync();

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
                IsCurrentUser = currentUserId.HasValue && p.UserId == currentUserId.Value
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
