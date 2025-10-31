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
        _logger.LogInformation("Strava callback received. Code present: {HasCode}, Error: {Error}", 
            !string.IsNullOrEmpty(code), error);

        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogError("Strava authorization error: {Error}", error);
            return Redirect($"{GetFrontendUrl()}/?error=access_denied");
        }

        if (string.IsNullOrEmpty(code))
        {
            _logger.LogError("Authorization code is missing");
            return Redirect($"{GetFrontendUrl()}/?error=no_code");
        }

        try
        {
            _logger.LogInformation("Exchanging code for token...");
            // Exchange code for token
            var tokenDto = await _stravaService.ExchangeCodeForTokenAsync(code);
            _logger.LogInformation("Token received for Strava ID: {StravaId}", tokenDto.Athlete?.Id);

            // Check if user already exists
            var existingUser = await _userService.GetUserByStravaIdAsync(tokenDto.Athlete?.Id ?? 0);

            if (existingUser != null)
            {
                _logger.LogInformation("Existing user found: {UserId}", existingUser.Id);
                // Update tokens
                await _userService.UpdateUserTokensAsync(existingUser, tokenDto);
                var redirectUrl = $"{GetFrontendUrl()}/dashboard?userId={existingUser.Id}";
                _logger.LogInformation("Redirecting to: {Url}", redirectUrl);
                return Redirect(redirectUrl);
            }
            else
            {
                _logger.LogInformation("Creating new user...");
                // Create new user
                var newUser = await _userService.CreateUserAsync(tokenDto);
                _logger.LogInformation("New user created: {UserId}", newUser.Id);
                var redirectUrl = $"{GetFrontendUrl()}/dashboard?userId={newUser.Id}";
                _logger.LogInformation("Redirecting to: {Url}", redirectUrl);
                return Redirect(redirectUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Strava callback: {Message}", ex.Message);
            return Redirect($"{GetFrontendUrl()}/?error=authentication_failed");
        }
    }

    private string GetFrontendUrl()
    {
        // Environment variable takes precedence over appsettings.json
        return Environment.GetEnvironmentVariable("FRONTEND_URL")
               ?? Configuration["Frontend:Url"] 
               ?? "http://localhost:5173";
    }

    private IConfiguration Configuration => HttpContext.RequestServices.GetRequiredService<IConfiguration>();
}

