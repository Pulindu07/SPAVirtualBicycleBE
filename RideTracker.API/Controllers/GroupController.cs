using Microsoft.AspNetCore.Mvc;
using RideTracker.Application.DTOs;
using RideTracker.Application.Interfaces;

namespace RideTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GroupController : ControllerBase
{
    private readonly IGroupService _groupService;
    private readonly IUserService _userService;
    private readonly ILogger<GroupController> _logger;

    public GroupController(
        IGroupService groupService, 
        IUserService userService,
        ILogger<GroupController> logger)
    {
        _groupService = groupService;
        _userService = userService;
        _logger = logger;
    }

    // GET: api/group/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<GroupDto>> GetGroup(int id)
    {
        try
        {
            var group = await _groupService.GetGroupByIdAsync(id);
            if (group == null)
                return NotFound(new { message = "Group not found" });

            return Ok(group);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting group {GroupId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the group" });
        }
    }

    // GET: api/group/user/{userId}
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<GroupDto>>> GetUserGroups(int userId)
    {
        try
        {
            var groups = await _groupService.GetUserGroupsAsync(userId);
            return Ok(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting groups for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while retrieving user groups" });
        }
    }

    // GET: api/group/all
    [HttpGet("all")]
    public async Task<ActionResult<List<GroupDto>>> GetAllGroups([FromQuery] int userId)
    {
        try
        {
            // Verify user is super admin
            var user = await _userService.GetUserDtoByIdAsync(userId);
            if (user == null || !user.IsSuperAdmin)
                return Unauthorized(new { message = "Only super admins can view all groups" });

            var groups = await _groupService.GetAllGroupsAsync();
            return Ok(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all groups");
            return StatusCode(500, new { message = "An error occurred while retrieving all groups" });
        }
    }

    // POST: api/group
    [HttpPost]
    public async Task<ActionResult<GroupDto>> CreateGroup([FromBody] CreateGroupRequest request)
    {
        try
        {
            var group = await _groupService.CreateGroupAsync(request.CreatorUserId, request.Group);
            return CreatedAtAction(nameof(GetGroup), new { id = group.Id }, group);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating group");
            return StatusCode(500, new { message = "An error occurred while creating the group" });
        }
    }

    // PUT: api/group/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<GroupDto>> UpdateGroup(int id, [FromBody] UpdateGroupRequest request)
    {
        try
        {
            var group = await _groupService.UpdateGroupAsync(id, request.UserId, request.Group);
            if (group == null)
                return NotFound(new { message = "Group not found or unauthorized" });

            return Ok(group);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating group {GroupId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the group" });
        }
    }

    // DELETE: api/group/{id}
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteGroup(int id, [FromQuery] int userId)
    {
        try
        {
            var result = await _groupService.DeleteGroupAsync(id, userId);
            if (!result)
                return NotFound(new { message = "Group not found or unauthorized" });

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting group {GroupId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the group" });
        }
    }

    // GET: api/group/{id}/members
    [HttpGet("{id}/members")]
    public async Task<ActionResult<List<GroupMemberDto>>> GetGroupMembers(int id)
    {
        try
        {
            var members = await _groupService.GetGroupMembersAsync(id);
            return Ok(members);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting members for group {GroupId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving group members" });
        }
    }

    // POST: api/group/{id}/members
    [HttpPost("{id}/members")]
    public async Task<ActionResult<GroupMemberDto>> AddMember(int id, [FromBody] AddMemberRequest request)
    {
        try
        {
            var member = await _groupService.AddMemberAsync(id, request.AdminUserId, request.Member);
            return CreatedAtAction(nameof(GetGroupMembers), new { id }, member);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding member to group {GroupId}", id);
            return StatusCode(500, new { message = "An error occurred while adding the member" });
        }
    }

    // DELETE: api/group/{id}/members/{memberId}
    [HttpDelete("{id}/members/{memberId}")]
    public async Task<ActionResult> RemoveMember(int id, int memberId, [FromQuery] int adminUserId)
    {
        try
        {
            var result = await _groupService.RemoveMemberAsync(id, adminUserId, memberId);
            if (!result)
                return NotFound(new { message = "Member not found or unauthorized" });

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing member {MemberId} from group {GroupId}", memberId, id);
            return StatusCode(500, new { message = "An error occurred while removing the member" });
        }
    }

    // PUT: api/group/{id}/members/{memberId}/role
    [HttpPut("{id}/members/{memberId}/role")]
    public async Task<ActionResult> UpdateMemberRole(int id, int memberId, [FromBody] UpdateRoleRequest request)
    {
        try
        {
            var result = await _groupService.UpdateMemberRoleAsync(id, request.AdminUserId, memberId, request.NewRole);
            if (!result)
                return NotFound(new { message = "Member not found or unauthorized" });

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role for member {MemberId} in group {GroupId}", memberId, id);
            return StatusCode(500, new { message = "An error occurred while updating the member role" });
        }
    }
}

// Request models
public record CreateGroupRequest(int CreatorUserId, CreateGroupDto Group);
public record UpdateGroupRequest(int UserId, UpdateGroupDto Group);
public record AddMemberRequest(int AdminUserId, AddGroupMemberDto Member);
public record UpdateRoleRequest(int AdminUserId, string NewRole);

