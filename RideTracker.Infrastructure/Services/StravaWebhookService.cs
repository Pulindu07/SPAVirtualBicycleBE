using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RideTracker.Application.Interfaces;

namespace RideTracker.Infrastructure.Services;

public class StravaWebhookService : IStravaWebhookService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<StravaWebhookService> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _callbackUrl;
    private readonly string _verifyToken;

    public StravaWebhookService(HttpClient httpClient, IConfiguration configuration, ILogger<StravaWebhookService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _clientId = Environment.GetEnvironmentVariable("STRAVA_CLIENT_ID")
                    ?? configuration["Strava:ClientId"]
                    ?? throw new InvalidOperationException("Strava ClientId not configured.");

        _clientSecret = Environment.GetEnvironmentVariable("STRAVA_CLIENT_SECRET")
                        ?? configuration["Strava:ClientSecret"]
                        ?? throw new InvalidOperationException("Strava ClientSecret not configured.");

        _callbackUrl = Environment.GetEnvironmentVariable("STRAVA_WEBHOOK_CALLBACK_URL")
                       ?? configuration["Strava:WebhookCallbackUrl"]
                       ?? throw new InvalidOperationException(
                           "Strava webhook callback URL not configured. Set STRAVA_WEBHOOK_CALLBACK_URL or Strava:WebhookCallbackUrl.");

        _verifyToken = Environment.GetEnvironmentVariable("STRAVA_WEBHOOK_VERIFY_TOKEN")
                       ?? configuration["Strava:WebhookVerifyToken"]
                       ?? throw new InvalidOperationException(
                           "Strava webhook verify token not configured. Set STRAVA_WEBHOOK_VERIFY_TOKEN or Strava:WebhookVerifyToken.");

        _httpClient.BaseAddress = new Uri("https://www.strava.com/api/v3/");
    }

    public string VerifyToken => _verifyToken;

    public async Task<long> CreateSubscriptionAsync()
    {
        var formData = new Dictionary<string, string>
        {
            { "client_id", _clientId },
            { "client_secret", _clientSecret },
            { "callback_url", _callbackUrl },
            { "verify_token", _verifyToken }
        };

        var response = await _httpClient.PostAsync("push_subscriptions", new FormUrlEncodedContent(formData));

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to create Strava subscription. Status: {Status}, Body: {Body}",
                response.StatusCode, errorContent);
            throw new HttpRequestException(
                $"Strava subscription create failed with status {response.StatusCode}. Response: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = result.GetProperty("id").GetInt64();
        _logger.LogInformation("Created Strava webhook subscription {SubscriptionId} for {CallbackUrl}", id, _callbackUrl);
        return id;
    }

    public async Task<StravaSubscriptionInfo?> GetSubscriptionAsync()
    {
        var url = $"push_subscriptions?client_id={Uri.EscapeDataString(_clientId)}&client_secret={Uri.EscapeDataString(_clientSecret)}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var subs = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        if (subs == null || subs.Length == 0)
        {
            return null;
        }

        var first = subs[0];
        return new StravaSubscriptionInfo
        {
            Id = first.GetProperty("id").GetInt64(),
            CallbackUrl = first.TryGetProperty("callback_url", out var cb) ? cb.GetString() ?? string.Empty : string.Empty,
            CreatedAt = first.TryGetProperty("created_at", out var ca) && ca.ValueKind == JsonValueKind.String
                ? DateTime.Parse(ca.GetString()!).ToUniversalTime() : null,
            UpdatedAt = first.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String
                ? DateTime.Parse(ua.GetString()!).ToUniversalTime() : null
        };
    }

    public async Task DeleteSubscriptionAsync(long id)
    {
        var url = $"push_subscriptions/{id}?client_id={Uri.EscapeDataString(_clientId)}&client_secret={Uri.EscapeDataString(_clientSecret)}";
        var response = await _httpClient.DeleteAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Strava subscription delete failed with status {response.StatusCode}. Response: {errorContent}");
        }

        _logger.LogInformation("Deleted Strava webhook subscription {SubscriptionId}", id);
    }
}
