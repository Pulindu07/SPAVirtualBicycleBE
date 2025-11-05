using RideTracker.Application.DTOs;

namespace RideTracker.Application.Interfaces;

public interface IGroupService
{
    Task<GroupDto?> GetGroupByIdAsync(int groupId);
    Task<List<GroupDto>> GetUserGroupsAsync(int userId);
    Task<GroupDto> CreateGroupAsync(int creatorUserId, CreateGroupDto dto);
    Task<GroupDto?> UpdateGroupAsync(int groupId, int userId, UpdateGroupDto dto);
    Task<bool> DeleteGroupAsync(int groupId, int userId);
    
    // Group Members
    Task<GroupMemberDto> AddMemberAsync(int groupId, int adminUserId, AddGroupMemberDto dto);
    Task<bool> RemoveMemberAsync(int groupId, int adminUserId, int memberUserId);
    Task<List<GroupMemberDto>> GetGroupMembersAsync(int groupId);
    Task<bool> UpdateMemberRoleAsync(int groupId, int adminUserId, int memberUserId, string newRole);
    Task<bool> IsUserGroupAdminAsync(int groupId, int userId);
}

