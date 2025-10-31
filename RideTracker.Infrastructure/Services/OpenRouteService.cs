using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RideTracker.Application.Interfaces;
using RideTracker.Domain.ValueObjects;

namespace RideTracker.Infrastructure.Services;

public class OpenRouteService : IRouteGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenRouteService> _logger;
    private readonly string? _apiKey;
    private const string BaseUrl = "https://api.openrouteservice.org/v2/directions/driving-car";

    public OpenRouteService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenRouteService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Environment variable takes precedence over appsettings.json
        _apiKey = Environment.GetEnvironmentVariable("OPENROUTE_API_KEY")
                  ?? configuration["OpenRouteService:ApiKey"];
    }

    public async Task<List<Coordinate>> GenerateRoadRouteAsync(List<Coordinate> waypoints)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("OpenRouteService API key not configured. Returning original waypoints.");
            return waypoints;
        }

        var detailedRoute = new List<Coordinate>();

        // Process waypoints in pairs (OpenRouteService handles routing between two points)
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            try
            {
                var segment = await GetRouteSegmentAsync(waypoints[i], waypoints[i + 1]);
                
                // Add segment points (skip first point if not the first segment to avoid duplicates)
                if (i == 0)
                {
                    detailedRoute.AddRange(segment);
                }
                else
                {
                    detailedRoute.AddRange(segment.Skip(1));
                }

                // Rate limiting: wait 2 seconds between requests to avoid rate limit (40 requests/minute = 1 per 1.5s minimum)
                if (i < waypoints.Count - 2)
                {
                    await Task.Delay(2000); // Increased to 2 seconds for safety
                }

                _logger.LogInformation("Generated route segment {Current}/{Total}", i + 1, waypoints.Count - 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate route segment from {Start} to {End}. Using direct line.", 
                    waypoints[i], waypoints[i + 1]);
                
                // Fallback: add the original waypoint
                if (i == 0)
                {
                    detailedRoute.Add(waypoints[i]);
                }
                detailedRoute.Add(waypoints[i + 1]);
            }
        }

        _logger.LogInformation("Generated detailed route with {Count} points from {Original} waypoints", 
            detailedRoute.Count, waypoints.Count);

        return detailedRoute;
    }

    private async Task<List<Coordinate>> GetRouteSegmentAsync(Coordinate start, Coordinate end)
    {
        var request = new
        {
            coordinates = new[]
            {
                new[] { start.Longitude, start.Latitude },  // OpenRouteService uses [lng, lat]
                new[] { end.Longitude, end.Latitude }
            },
            radiuses = new[] { 5000, 5000 }  // Increase search radius to 5km to find nearest road
        };

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", _apiKey);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");

        var response = await _httpClient.PostAsJsonAsync(BaseUrl, request);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"OpenRouteService API error: {response.StatusCode} - {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        
        var geometry = result.GetProperty("routes")[0].GetProperty("geometry");
        var coordinates = DecodePolyline(geometry.GetString() ?? string.Empty);
        
        return coordinates.Select(coord => new Coordinate(coord.Latitude, coord.Longitude)).ToList();
    }

    private List<(double Latitude, double Longitude)> DecodePolyline(string encodedPolyline)
    {
        if (string.IsNullOrEmpty(encodedPolyline))
            return new List<(double, double)>();

        var points = new List<(double, double)>();
        int index = 0;
        int lat = 0, lng = 0;

        while (index < encodedPolyline.Length)
        {
            int result = 1;
            int shift = 0;
            int b;
            do
            {
                b = encodedPolyline[index++] - 63 - 1;
                result += b << shift;
                shift += 5;
            } while (b >= 0x1f);
            lat += (result & 1) != 0 ? ~(result >> 1) : result >> 1;

            result = 1;
            shift = 0;
            do
            {
                b = encodedPolyline[index++] - 63 - 1;
                result += b << shift;
                shift += 5;
            } while (b >= 0x1f);
            lng += (result & 1) != 0 ? ~(result >> 1) : result >> 1;

            points.Add((lat / 1E5, lng / 1E5));
        }

        return points;
    }
}

