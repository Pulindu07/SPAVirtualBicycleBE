using Microsoft.EntityFrameworkCore;
using RideTracker.Application.DTOs;
using RideTracker.Application.Interfaces;
using RideTracker.Domain.Entities;
using RideTracker.Infrastructure.Data;

namespace RideTracker.Infrastructure.Services;

public class GroupService : IGroupService
{
    private readonly RideTrackerDbContext _context;

    public GroupService(RideTrackerDbContext context)
    {
        _context = context;
    }

    public async Task<GroupDto?> GetGroupByIdAsync(int groupId)
    {
        var group = await _context.Groups
            .Include(g => g.CreatedBy)
            .Include(g => g.Members)
                .ThenInclude(m => m.User)
            .Where(g => g.Id == groupId && g.IsActive)
            .FirstOrDefaultAsync();

        if (group == null) return null;

        return new GroupDto
        {
            Id = group.Id,
            Name = group.Name,
            IconUrl = group.IconUrl,
            CreatedByUserId = group.CreatedByUserId,
            CreatedByUsername = group.CreatedBy.Username,
            CreatedAt = group.CreatedAt,
            MemberCount = group.Members.Count(m => m.IsActive),
            Members = group.Members
                .Where(m => m.IsActive)
                .Select(m => new GroupMemberDto
                {
                    Id = m.Id,
                    GroupId = m.GroupId,
                    UserId = m.UserId,
                    Username = m.User.Username,
                    FirstName = m.User.FirstName,
                    LastName = m.User.LastName,
                    Role = m.Role,
                    JoinedAt = m.JoinedAt,
                    TotalDistanceKm = m.User.TotalDistanceKm
                })
                .OrderByDescending(m => m.Role) // admins first
                .ThenBy(m => m.JoinedAt)
                .ToList()
        };
    }

    public async Task<List<GroupDto>> GetUserGroupsAsync(int userId)
    {
        var groups = await _context.GroupMembers
            .Include(gm => gm.Group)
                .ThenInclude(g => g.CreatedBy)
            .Include(gm => gm.Group)
                .ThenInclude(g => g.Members)
                    .ThenInclude(m => m.User)
            .Where(gm => gm.UserId == userId && gm.IsActive && gm.Group.IsActive)
            .Select(gm => gm.Group)
            .ToListAsync();

        return groups.Select(g => new GroupDto
        {
            Id = g.Id,
            Name = g.Name,
            IconUrl = g.IconUrl,
            CreatedByUserId = g.CreatedByUserId,
            CreatedByUsername = g.CreatedBy.Username,
            CreatedAt = g.CreatedAt,
            MemberCount = g.Members.Count(m => m.IsActive),
            Members = g.Members
                .Where(m => m.IsActive)
                .Select(m => new GroupMemberDto
                {
                    Id = m.Id,
                    GroupId = m.GroupId,
                    UserId = m.UserId,
                    Username = m.User.Username,
                    FirstName = m.User.FirstName,
                    LastName = m.User.LastName,
                    Role = m.Role,
                    JoinedAt = m.JoinedAt,
                    TotalDistanceKm = m.User.TotalDistanceKm
                })
                .ToList()
        }).ToList();
    }

    public async Task<List<GroupDto>> GetAllGroupsAsync()
    {
        var groups = await _context.Groups
            .Include(g => g.CreatedBy)
            .Include(g => g.Members)
                .ThenInclude(m => m.User)
            .Where(g => g.IsActive)
            .OrderBy(g => g.Name)
            .ToListAsync();

        return groups.Select(g => new GroupDto
        {
            Id = g.Id,
            Name = g.Name,
            IconUrl = g.IconUrl,
            CreatedByUserId = g.CreatedByUserId,
            CreatedByUsername = g.CreatedBy.Username,
            CreatedAt = g.CreatedAt,
            MemberCount = g.Members.Count(m => m.IsActive),
            Members = g.Members
                .Where(m => m.IsActive)
                .Select(m => new GroupMemberDto
                {
                    Id = m.Id,
                    GroupId = m.GroupId,
                    UserId = m.UserId,
                    Username = m.User.Username,
                    FirstName = m.User.FirstName,
                    LastName = m.User.LastName,
                    Role = m.Role,
                    JoinedAt = m.JoinedAt,
                    TotalDistanceKm = m.User.TotalDistanceKm
                })
                .ToList()
        }).ToList();
    }

    public async Task<GroupDto> CreateGroupAsync(int creatorUserId, CreateGroupDto dto)
    {
        // Only super admins can create groups
        var creator = await _context.Users.FindAsync(creatorUserId);
        if (creator == null || !creator.IsSuperAdmin)
            throw new UnauthorizedAccessException("Only super admins can create groups");

        var group = new Group
        {
            Name = dto.Name,
            IconUrl = dto.IconUrl,
            CreatedByUserId = creatorUserId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Groups.Add(group);
        await _context.SaveChangesAsync();

        // Add creator as admin member
        var creatorMember = new GroupMember
        {
            GroupId = group.Id,
            UserId = creatorUserId,
            Role = "admin",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.GroupMembers.Add(creatorMember);
        await _context.SaveChangesAsync();

        return (await GetGroupByIdAsync(group.Id))!;
    }

    public async Task<GroupDto?> UpdateGroupAsync(int groupId, int userId, UpdateGroupDto dto)
    {
        var group = await _context.Groups.FindAsync(groupId);
        if (group == null || !group.IsActive) return null;

        // Check if user is admin
        if (!await IsUserGroupAdminAsync(groupId, userId))
            return null;

        if (!string.IsNullOrWhiteSpace(dto.Name))
            group.Name = dto.Name;

        if (dto.IconUrl != null)
            group.IconUrl = dto.IconUrl;

        await _context.SaveChangesAsync();

        return await GetGroupByIdAsync(groupId);
    }

    public async Task<bool> DeleteGroupAsync(int groupId, int userId)
    {
        var group = await _context.Groups.FindAsync(groupId);
        if (group == null || !group.IsActive) return false;

        // Only creator can delete
        if (group.CreatedByUserId != userId)
            return false;

        group.IsActive = false;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<GroupMemberDto> AddMemberAsync(int groupId, int adminUserId, AddGroupMemberDto dto)
    {
        // Check if user is a group admin or super admin
        var adminUser = await _context.Users.FindAsync(adminUserId);
        if (adminUser == null)
            throw new UnauthorizedAccessException("User not found");

        var isGroupAdmin = await IsUserGroupAdminAsync(groupId, adminUserId);
        if (!isGroupAdmin && !adminUser.IsSuperAdmin)
            throw new UnauthorizedAccessException("Only group admins or super admins can add members");

        // Check if user is already a member
        var existingMember = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == dto.UserId);

        if (existingMember != null)
        {
            if (existingMember.IsActive)
                throw new InvalidOperationException("User is already a member");
            
            // Reactivate membership
            existingMember.IsActive = true;
            existingMember.JoinedAt = DateTime.UtcNow;
        }
        else
        {
            // Determine role: super admins are automatically admins, otherwise use provided role
            var newMemberUser = await _context.Users.FindAsync(dto.UserId);
            var memberRole = newMemberUser?.IsSuperAdmin == true ? "admin" : dto.Role;

            existingMember = new GroupMember
            {
                GroupId = groupId,
                UserId = dto.UserId,
                Role = memberRole,
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            };
            _context.GroupMembers.Add(existingMember);
        }

        await _context.SaveChangesAsync();

        var user = await _context.Users.FindAsync(dto.UserId);
        return new GroupMemberDto
        {
            Id = existingMember.Id,
            GroupId = existingMember.GroupId,
            UserId = existingMember.UserId,
            Username = user!.Username,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = existingMember.Role,
            JoinedAt = existingMember.JoinedAt,
            TotalDistanceKm = user.TotalDistanceKm
        };
    }

    public async Task<bool> RemoveMemberAsync(int groupId, int adminUserId, int memberUserId)
    {
        if (!await IsUserGroupAdminAsync(groupId, adminUserId))
            return false;

        var member = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == memberUserId && gm.IsActive);

        if (member == null) return false;

        // Can't remove the group creator
        var group = await _context.Groups.FindAsync(groupId);
        if (group!.CreatedByUserId == memberUserId)
            return false;

        member.IsActive = false;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<List<GroupMemberDto>> GetGroupMembersAsync(int groupId)
    {
        var members = await _context.GroupMembers
            .Include(gm => gm.User)
            .Where(gm => gm.GroupId == groupId && gm.IsActive)
            .OrderByDescending(gm => gm.Role)
            .ThenBy(gm => gm.JoinedAt)
            .ToListAsync();

        return members.Select(m => new GroupMemberDto
        {
            Id = m.Id,
            GroupId = m.GroupId,
            UserId = m.UserId,
            Username = m.User.Username,
            FirstName = m.User.FirstName,
            LastName = m.User.LastName,
            Role = m.Role,
            JoinedAt = m.JoinedAt,
            TotalDistanceKm = m.User.TotalDistanceKm
        }).ToList();
    }

    public async Task<bool> UpdateMemberRoleAsync(int groupId, int adminUserId, int memberUserId, string newRole)
    {
        if (!await IsUserGroupAdminAsync(groupId, adminUserId))
            return false;

        var member = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == memberUserId && gm.IsActive);

        if (member == null) return false;

        // Can't change the role of the group creator
        var group = await _context.Groups.FindAsync(groupId);
        if (group!.CreatedByUserId == memberUserId)
            return false;

        member.Role = newRole;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> IsUserGroupAdminAsync(int groupId, int userId)
    {
        // Check if user is super admin
        var user = await _context.Users.FindAsync(userId);
        if (user?.IsSuperAdmin == true)
            return true;

        // Check if user is group admin
        var member = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId && gm.IsActive);

        return member?.Role == "admin";
    }
}

