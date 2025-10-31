using Microsoft.AspNetCore.Mvc;
using RideTracker.Application.Interfaces;

namespace RideTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IStravaService _stravaService;
    private readonly IUserService _userService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IStravaService stravaService,
        IUserService userService,
        ILogger<AuthController> logger)
    {
        _stravaService = stravaService;
        _userService = userService;
        _logger = logger;
    }

    [HttpGet("strava/login")]
    public IActionResult StravaLogin()
    {
        var authUrl = _stravaService.GetAuthorizationUrl();
        return Redirect(authUrl);
    }

    [HttpGet("strava/callback")]
    public async Task<IActionResult> StravaCallback([FromQuery] string code, [FromQuery] string? error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogError("Strava authorization error: {Error}", error);
            return Redirect($"{GetFrontendUrl()}/login?error=access_denied");
        }

        if (string.IsNullOrEmpty(code))
        {
            return BadRequest("Authorization code is missing");
        }

        try
        {
            // Exchange code for token
            var tokenDto = await _stravaService.ExchangeCodeForTokenAsync(code);

            // Check if user already exists
            var existingUser = await _userService.GetUserByStravaIdAsync(tokenDto.Athlete?.Id ?? 0);

            if (existingUser != null)
            {
                // Update tokens
                await _userService.UpdateUserTokensAsync(existingUser, tokenDto);
                return Redirect($"{GetFrontendUrl()}/dashboard?userId={existingUser.Id}");
            }
            else
            {
                // Create new user
                var newUser = await _userService.CreateUserAsync(tokenDto);
                return Redirect($"{GetFrontendUrl()}/dashboard?userId={newUser.Id}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Strava callback");
            return Redirect($"{GetFrontendUrl()}/login?error=authentication_failed");
        }
    }

    private string GetFrontendUrl()
    {
        return Configuration["Frontend:Url"] ?? "http://localhost:5173";
    }

    private IConfiguration Configuration => HttpContext.RequestServices.GetRequiredService<IConfiguration>();
}

