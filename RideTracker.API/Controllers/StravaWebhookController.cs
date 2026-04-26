using Hangfire;
using Microsoft.AspNetCore.Mvc;
using RideTracker.Application.DTOs;
using RideTracker.Application.Interfaces;

namespace RideTracker.API.Controllers;

[ApiController]
[Route("api/webhooks/strava")]
public class StravaWebhookController : ControllerBase
{
    private readonly IStravaWebhookService _webhookService;
    private readonly IUserService _userService;
    private readonly ILogger<StravaWebhookController> _logger;

    public StravaWebhookController(
        IStravaWebhookService webhookService,
        IUserService userService,
        ILogger<StravaWebhookController> logger)
    {
        _webhookService = webhookService;
        _userService = userService;
        _logger = logger;
    }

    // Strava handshake. Strava calls this on subscription create with hub.* query params
    // and expects { "hub.challenge": "<value>" } JSON back.
    [HttpGet]
    public IActionResult Handshake(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (!string.Equals(mode, "subscribe", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "invalid mode" });
        }

        if (string.IsNullOrEmpty(verifyToken) || verifyToken != _webhookService.VerifyToken)
        {
            _logger.LogWarning("Strava webhook handshake failed: bad verify token.");
            return Forbid();
        }

        if (string.IsNullOrEmpty(challenge))
        {
            return BadRequest(new { error = "missing challenge" });
        }

        return Ok(new Dictionary<string, string> { { "hub.challenge", challenge } });
    }

    // Strava event receiver. Must respond 200 within ~2s; defer real work to Hangfire.
    [HttpPost]
    public IActionResult Receive([FromBody] StravaWebhookEventDto evt)
    {
        if (evt == null)
        {
            return BadRequest();
        }

        _logger.LogInformation(
            "Received Strava webhook: {ObjectType} {ObjectId} {AspectType} owner={OwnerId} sub={SubscriptionId}",
            evt.ObjectType, evt.ObjectId, evt.AspectType, evt.OwnerId, evt.SubscriptionId);

        BackgroundJob.Enqueue<IStravaWebhookProcessor>(p => p.ProcessEventAsync(evt));
        return Ok();
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromQuery] int adminUserId)
    {
        if (!await IsSuperAdminAsync(adminUserId))
        {
            return Forbid();
        }

        try
        {
            var id = await _webhookService.CreateSubscriptionAsync();
            return Ok(new { subscriptionId = id });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to create Strava webhook subscription");
            return StatusCode(502, new { message = ex.Message });
        }
    }

    [HttpGet("subscription")]
    public async Task<IActionResult> GetSubscription([FromQuery] int adminUserId)
    {
        if (!await IsSuperAdminAsync(adminUserId))
        {
            return Forbid();
        }

        var sub = await _webhookService.GetSubscriptionAsync();
        if (sub == null)
        {
            return Ok(new { active = false });
        }
        return Ok(new { active = true, subscription = sub });
    }

    [HttpDelete("subscription/{id:long}")]
    public async Task<IActionResult> DeleteSubscription(long id, [FromQuery] int adminUserId)
    {
        if (!await IsSuperAdminAsync(adminUserId))
        {
            return Forbid();
        }

        try
        {
            await _webhookService.DeleteSubscriptionAsync(id);
            return Ok(new { deleted = id });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to delete Strava webhook subscription {Id}", id);
            return StatusCode(502, new { message = ex.Message });
        }
    }

    private async Task<bool> IsSuperAdminAsync(int userId)
    {
        var user = await _userService.GetUserByIdAsync(userId);
        return user != null && user.IsActive && user.IsSuperAdmin;
    }
}
