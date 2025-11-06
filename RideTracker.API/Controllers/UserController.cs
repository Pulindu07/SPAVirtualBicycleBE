using Microsoft.AspNetCore.Mvc;
using RideTracker.Application.Interfaces;

namespace RideTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ISyncService _syncService;
    private readonly ILogger<UserController> _logger;

    public UserController(
        IUserService userService,
        ISyncService syncService,
        ILogger<UserController> logger)
    {
        _userService = userService;
        _syncService = syncService;
        _logger = logger;
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetUser(int userId)
    {
        try
        {
            var user = await _userService.GetUserDtoByIdAsync(userId);
            
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while retrieving user information" });
        }
    }

    [HttpGet("{userId}/progress")]
    public async Task<IActionResult> GetProgress(int userId)
    {
        try
        {
            var progress = await _userService.GetUserProgressAsync(userId);
            
            if (progress == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting progress for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while retrieving user progress" });
        }
    }

    [HttpPost("{userId}/sync")]
    public async Task<IActionResult> SyncActivities(int userId)
    {
        try
        {
            await _syncService.SyncUserActivitiesAsync(userId);
            return Ok(new { message = "Sync completed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing activities for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred during sync" });
        }
    }
}

