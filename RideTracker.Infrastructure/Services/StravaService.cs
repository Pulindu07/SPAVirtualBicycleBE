using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RideTracker.Application.DTOs;
using RideTracker.Application.Interfaces;
using RideTracker.Domain.Entities;

namespace RideTracker.Infrastructure.Services;

public class StravaService : IStravaService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _redirectUri;

    public StravaService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;

        // Environment variables take precedence over appsettings.json
        _clientId = Environment.GetEnvironmentVariable("STRAVA_CLIENT_ID")
                    ?? configuration["Strava:ClientId"]
                    ?? throw new InvalidOperationException("Strava ClientId not configured. Set STRAVA_CLIENT_ID env var or Strava:ClientId in appsettings.json");

        _clientSecret = Environment.GetEnvironmentVariable("STRAVA_CLIENT_SECRET")
                        ?? configuration["Strava:ClientSecret"]
                        ?? throw new InvalidOperationException("Strava ClientSecret not configured. Set STRAVA_CLIENT_SECRET env var or Strava:ClientSecret in appsettings.json");

        _redirectUri = Environment.GetEnvironmentVariable("STRAVA_REDIRECT_URI")
                       ?? configuration["Strava:RedirectUri"]
                       ?? throw new InvalidOperationException("Strava RedirectUri not configured. Set STRAVA_REDIRECT_URI env var or Strava:RedirectUri in appsettings.json");

        _httpClient.BaseAddress = new Uri("https://www.strava.com/api/v3/");
    }

    public string GetAuthorizationUrl()
    {
        return $"https://www.strava.com/oauth/authorize?client_id={_clientId}&response_type=code&redirect_uri={_redirectUri}&approval_prompt=force&scope=activity:read_all";
    }

    public async Task<StravaTokenDto> ExchangeCodeForTokenAsync(string code)
    {
        var response = await _httpClient.PostAsJsonAsync("https://www.strava.com/oauth/token", new
        {
            client_id = _clientId,
            client_secret = _clientSecret,
            code = code,
            grant_type = "authorization_code"
        });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        return new StravaTokenDto
        {
            AccessToken = result.GetProperty("access_token").GetString() ?? string.Empty,
            RefreshToken = result.GetProperty("refresh_token").GetString() ?? string.Empty,
            ExpiresIn = result.GetProperty("expires_in").GetInt32(),
            ExpiresAt = result.GetProperty("expires_at").GetInt64() > 0
                ? DateTime.SpecifyKind(DateTimeOffset.FromUnixTimeSeconds(result.GetProperty("expires_at").GetInt64()).DateTime, DateTimeKind.Utc)
                : DateTime.UtcNow.AddSeconds(result.GetProperty("expires_in").GetInt32()),
            Athlete = new StravaAthleteDto
            {
                Id = result.GetProperty("athlete").GetProperty("id").GetInt64(),
                Username = result.GetProperty("athlete").TryGetProperty("username", out var username) ? username.GetString() ?? string.Empty : string.Empty,
                Firstname = result.GetProperty("athlete").TryGetProperty("firstname", out var firstname) ? firstname.GetString() ?? string.Empty : string.Empty,
                Lastname = result.GetProperty("athlete").TryGetProperty("lastname", out var lastname) ? lastname.GetString() ?? string.Empty : string.Empty
            }
        };
    }

    public async Task<StravaTokenDto> RefreshTokenAsync(string refreshToken)
    {
        var response = await _httpClient.PostAsJsonAsync("https://www.strava.com/oauth/token", new
        {
            client_id = _clientId,
            client_secret = _clientSecret,
            refresh_token = refreshToken,
            grant_type = "refresh_token"
        });

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Strava token refresh failed with status {response.StatusCode}. Response: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        return new StravaTokenDto
        {
            AccessToken = result.GetProperty("access_token").GetString() ?? string.Empty,
            RefreshToken = result.GetProperty("refresh_token").GetString() ?? string.Empty,
            ExpiresIn = result.GetProperty("expires_in").GetInt32(),
            ExpiresAt = result.GetProperty("expires_at").GetInt64() > 0
                ? DateTime.SpecifyKind(DateTimeOffset.FromUnixTimeSeconds(result.GetProperty("expires_at").GetInt64()).DateTime, DateTimeKind.Utc)
                : DateTime.UtcNow.AddSeconds(result.GetProperty("expires_in").GetInt32())
        };
    }

    public async Task<List<StravaActivityDto>> GetActivitiesAfterAsync(string accessToken, DateTime after)
    {
        // Create a request message to avoid modifying shared DefaultRequestHeaders
        var request = new HttpRequestMessage(HttpMethod.Get, $"athlete/activities?after={new DateTimeOffset(after).ToUnixTimeSeconds()}&per_page=200");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var activities = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        return activities?.Select(a => new StravaActivityDto
        {
            Id = a.GetProperty("id").GetInt64(),
            Name = a.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
            Distance = a.GetProperty("distance").GetDouble(),
            MovingTime = a.GetProperty("moving_time").GetInt64(),
            StartDate = a.GetProperty("start_date").GetDateTime(),
            AverageSpeed = a.TryGetProperty("average_speed", out var avgSpeed) ? avgSpeed.GetDouble() : 0,
            Type = a.TryGetProperty("type", out var type) ? type.GetString() ?? string.Empty : string.Empty,
            Manual = a.TryGetProperty("manual", out var manual) ? manual.GetBoolean() : false
        }).Where(a => a.Distance > 0 && a.Type.Equals("Ride", StringComparison.OrdinalIgnoreCase) && !a.Manual).ToList() ?? new List<StravaActivityDto>();
    }

    public async Task<bool> RefreshTokenIfNeededAsync(User user)
    {
        if (string.IsNullOrWhiteSpace(user.RefreshToken))
        {
            throw new InvalidOperationException($"User {user.Id} has no refresh token. User needs to re-authenticate with Strava.");
        }

        if (user.TokenExpiry <= DateTime.UtcNow.AddMinutes(5))
        {
            var newToken = await RefreshTokenAsync(user.RefreshToken);
            user.AccessToken = newToken.AccessToken;
            user.RefreshToken = newToken.RefreshToken;
            user.TokenExpiry = DateTime.SpecifyKind(newToken.ExpiresAt, DateTimeKind.Utc);
            return true;
        }
        return false;
    }
}

