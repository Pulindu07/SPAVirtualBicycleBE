using Microsoft.AspNetCore.Mvc;
using RideTracker.Application.DTOs;
using RideTracker.Application.Interfaces;

namespace RideTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChallengeController : ControllerBase
{
    private readonly IChallengeService _challengeService;
    private readonly ISyncService _syncService;
    private readonly ILogger<ChallengeController> _logger;

    public ChallengeController(
        IChallengeService challengeService, 
        ISyncService syncService,
        ILogger<ChallengeController> logger)
    {
        _challengeService = challengeService;
        _syncService = syncService;
        _logger = logger;
    }

    // GET: api/challenge/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<ChallengeDto>> GetChallenge(int id, [FromQuery] int? userId = null)
    {
        try
        {
            var challenge = await _challengeService.GetChallengeByIdAsync(id, userId);
            if (challenge == null)
                return NotFound(new { message = "Challenge not found" });

            return Ok(challenge);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting challenge {ChallengeId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the challenge" });
        }
    }

    // GET: api/challenge/user/{userId}
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<ChallengeDto>>> GetUserChallenges(int userId)
    {
        try
        {
            var challenges = await _challengeService.GetUserChallengesAsync(userId);
            return Ok(challenges);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting challenges for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while retrieving user challenges" });
        }
    }

    // GET: api/challenge/group/{groupId}
    [HttpGet("group/{groupId}")]
    public async Task<ActionResult<List<ChallengeDto>>> GetGroupChallenges(int groupId)
    {
        try
        {
            var challenges = await _challengeService.GetGroupChallengesAsync(groupId);
            return Ok(challenges);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting challenges for group {GroupId}", groupId);
            return StatusCode(500, new { message = "An error occurred while retrieving group challenges" });
        }
    }

    // POST: api/challenge
    [HttpPost]
    public async Task<ActionResult<ChallengeDto>> CreateChallenge([FromBody] CreateChallengeRequest request)
    {
        try
        {
            var challenge = await _challengeService.CreateChallengeAsync(request.CreatorUserId, request.Challenge);
            return CreatedAtAction(nameof(GetChallenge), new { id = challenge.Id }, challenge);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating challenge");
            return StatusCode(500, new { message = "An error occurred while creating the challenge" });
        }
    }

    // PUT: api/challenge/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<ChallengeDto>> UpdateChallenge(int id, [FromBody] UpdateChallengeRequest request)
    {
        try
        {
            var challenge = await _challengeService.UpdateChallengeAsync(id, request.UserId, request.Challenge);
            if (challenge == null)
                return NotFound(new { message = "Challenge not found or unauthorized" });

            return Ok(challenge);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating challenge {ChallengeId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the challenge" });
        }
    }

    // DELETE: api/challenge/{id}
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteChallenge(int id, [FromQuery] int userId)
    {
        try
        {
            var result = await _challengeService.DeleteChallengeAsync(id, userId);
            if (!result)
                return NotFound(new { message = "Challenge not found or unauthorized" });

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting challenge {ChallengeId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the challenge" });
        }
    }

    // POST: api/challenge/{id}/join
    [HttpPost("{id}/join")]
    public async Task<ActionResult> JoinChallenge(int id, [FromBody] JoinChallengeRequest request)
    {
        try
        {
            var result = await _challengeService.JoinChallengeAsync(id, request.UserId);
            if (!result)
                return BadRequest(new { message = "Unable to join challenge" });

            return Ok(new { message = "Successfully joined challenge" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining challenge {ChallengeId}", id);
            return StatusCode(500, new { message = "An error occurred while joining the challenge" });
        }
    }

    // POST: api/challenge/{id}/leave
    [HttpPost("{id}/leave")]
    public async Task<ActionResult> LeaveChallenge(int id, [FromBody] LeaveChallengeRequest request)
    {
        try
        {
            var result = await _challengeService.LeaveChallengeAsync(id, request.UserId);
            if (!result)
                return BadRequest(new { message = "Unable to leave challenge" });

            return Ok(new { message = "Successfully left challenge" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving challenge {ChallengeId}", id);
            return StatusCode(500, new { message = "An error occurred while leaving the challenge" });
        }
    }

    // GET: api/challenge/{id}/progress/{userId}
    [HttpGet("{id}/progress/{userId}")]
    public async Task<ActionResult<ChallengeProgressDto>> GetUserProgress(int id, int userId)
    {
        try
        {
            var progress = await _challengeService.GetUserChallengeProgressAsync(id, userId);
            if (progress == null)
                return NotFound(new { message = "Progress not found" });

            return Ok(progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting progress for user {UserId} in challenge {ChallengeId}", userId, id);
            return StatusCode(500, new { message = "An error occurred while retrieving progress" });
        }
    }

    // GET: api/challenge/{id}/progress/group
    [HttpGet("{id}/progress/group")]
    public async Task<ActionResult<GroupChallengeProgressDto>> GetGroupProgress(int id, [FromQuery] int? userId = null)
    {
        try
        {
            var progress = await _challengeService.GetGroupChallengeProgressAsync(id, userId);
            if (progress == null)
                return NotFound(new { message = "Group progress not found" });

            return Ok(progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting group progress for challenge {ChallengeId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving group progress" });
        }
    }

    // POST: api/challenge/{id}/update-progress/{userId}
    [HttpPost("{id}/update-progress/{userId}")]
    public async Task<ActionResult> UpdateProgress(int id, int userId)
    {
        try
        {
            await _challengeService.UpdateChallengeProgressAsync(id, userId);
            return Ok(new { message = "Progress updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating progress for user {UserId} in challenge {ChallengeId}", userId, id);
            return StatusCode(500, new { message = "An error occurred while updating progress" });
        }
    }

    // GET: api/challenge/{id}/leaderboard
    [HttpGet("{id}/leaderboard")]
    public async Task<ActionResult<LeaderboardDto>> GetLeaderboard(int id, [FromQuery] int? userId = null)
    {
        try
        {
            var leaderboard = await _challengeService.GetChallengeLeaderboardAsync(id, userId);
            return Ok(leaderboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting leaderboard for challenge {ChallengeId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the leaderboard" });
        }
    }

    // GET: api/challenge/{id}/inter-group-leaderboard
    [HttpGet("{id}/inter-group-leaderboard")]
    public async Task<ActionResult<InterGroupLeaderboardDto>> GetInterGroupLeaderboard(int id, [FromQuery] int? userId = null)
    {
        try
        {
            var leaderboard = await _challengeService.GetInterGroupLeaderboardAsync(id, userId);
            return Ok(leaderboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inter-group leaderboard for challenge {ChallengeId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the inter-group leaderboard" });
        }
    }

    // POST: api/challenge/{id}/sync-group
    [HttpPost("{id}/sync-group")]
    public async Task<ActionResult> SyncGroupChallenge(int id)
    {
        try
        {
            await _syncService.SyncGroupChallengeAsync(id);
            return Ok(new { message = "Group challenge sync completed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing group challenge {ChallengeId}", id);
            return StatusCode(500, new { message = "An error occurred while syncing the group challenge" });
        }
    }

    // POST: api/challenge/{id}/sync-inter-group
    [HttpPost("{id}/sync-inter-group")]
    public async Task<ActionResult> SyncInterGroupChallenge(int id)
    {
        try
        {
            await _syncService.SyncInterGroupChallengeAsync(id);
            return Ok(new { message = "Inter-group challenge sync completed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing inter-group challenge {ChallengeId}", id);
            return StatusCode(500, new { message = "An error occurred while syncing the inter-group challenge" });
        }
    }
}

// Request models
public record CreateChallengeRequest(int CreatorUserId, CreateChallengeDto Challenge);
public record UpdateChallengeRequest(int UserId, UpdateChallengeDto Challenge);
public record JoinChallengeRequest(int UserId);
public record LeaveChallengeRequest(int UserId);

