using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RideTracker.Application.Interfaces;
using RideTracker.Domain.ValueObjects;

namespace RideTracker.Infrastructure.Services;

public class GoogleMapsRouteService : IRouteGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleMapsRouteService> _logger;
    private readonly string? _apiKey;
    private const string DirectionsBaseUrl = "https://maps.googleapis.com/maps/api/directions/json";

    public GoogleMapsRouteService(
        HttpClient httpClient, 
        IConfiguration configuration, 
        ILogger<GoogleMapsRouteService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY")
                  ?? configuration["GoogleMaps:ApiKey"];
    }

    public async Task<List<Coordinate>> GenerateRoadRouteAsync(List<Coordinate> waypoints)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogError("❌ Google Maps API key is NOT configured!");
            _logger.LogError("Set GOOGLE_MAPS_API_KEY environment variable or GoogleMaps:ApiKey in appsettings.json");
            throw new InvalidOperationException("Google Maps API key is required for route generation");
        }

        // Log masked API key for verification (show first 10 and last 5 chars)
        var maskedKey = _apiKey.Length > 15 
            ? $"{_apiKey.Substring(0, 10)}...{_apiKey.Substring(_apiKey.Length - 5)}"
            : "***";
        _logger.LogInformation("✅ Using Google Maps API key: {MaskedKey}", maskedKey);

        if (waypoints.Count < 2)
        {
            _logger.LogWarning("Need at least 2 waypoints to generate a route.");
            return waypoints;
        }

        _logger.LogInformation("🗺️ Starting route generation with Google Maps Directions API...");
        _logger.LogInformation("📍 Processing {Count} base waypoints segment by segment for maximum accuracy.", waypoints.Count);

        var detailedRoute = new List<Coordinate>();
        
        // Process route segment by segment (pair of points) to isolate issues and improve accuracy
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            var startPoint = waypoints[i];
            var endPoint = waypoints[i + 1];
            var segmentWaypoints = new List<Coordinate> { startPoint, endPoint };

            try
            {
                _logger.LogInformation("Processing segment {Current}/{Total}: {StartLat},{StartLng} -> {EndLat},{EndLng}", 
                    i + 1, waypoints.Count - 1, startPoint.Latitude, startPoint.Longitude, endPoint.Latitude, endPoint.Longitude);

                var segment = await GetRouteSegmentAsync(segmentWaypoints);
                
                if (i == 0)
                {
                    detailedRoute.AddRange(segment);
                }
                else
                {
                    // Skip the first point of the new segment to avoid duplicating the end point of the last segment
                    detailedRoute.AddRange(segment.Skip(1));
                }

                _logger.LogInformation("Segment {Current}/{Total} complete. Total points so far: {TotalPoints}", 
                    i + 1, waypoints.Count - 1, detailedRoute.Count);

                // Short delay to respect API rate limits (50 QPS is the default limit)
                await Task.Delay(50); 
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "❌ Google Maps API request failed for segment {Current}/{Total}. Error: {Message}", 
                    i + 1, waypoints.Count - 1, ex.Message);
                _logger.LogError("Problematic segment: from {StartLat},{StartLng} to {EndLat},{EndLng}", 
                    startPoint.Latitude, startPoint.Longitude, endPoint.Latitude, endPoint.Longitude);
                _logger.LogError("⚠️ This usually means no route exists between these two points for the selected travel mode.");

                // Re-throw to indicate failure
                throw new InvalidOperationException(
                    $"Google Maps route generation failed on segment {i+1}. Check coordinates near {startPoint.Latitude},{startPoint.Longitude}. Original error: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Unexpected error during route generation for segment {Current}/{Total}", 
                    i + 1, waypoints.Count - 1);
                throw new InvalidOperationException($"Route generation failed unexpectedly on segment {i+1}: {ex.Message}", ex);
            }
        }

        _logger.LogInformation("✅ Route generation complete! Generated {Count} detailed points from {Original} waypoints", 
            detailedRoute.Count, waypoints.Count);

        return detailedRoute;
    }

    private async Task<List<Coordinate>> GetRouteSegmentAsync(List<Coordinate> waypoints)
    {
        if (waypoints.Count < 2)
            return waypoints;

        var origin = $"{waypoints[0].Latitude},{waypoints[0].Longitude}";
        var destination = $"{waypoints[^1].Latitude},{waypoints[^1].Longitude}";
        
        var waypointsParam = string.Empty;
        if (waypoints.Count > 2)
        {
            // Google expects waypoints as "lat,lng|lat,lng|..."
            waypointsParam = "&waypoints=" + string.Join("|", waypoints.Skip(1).Take(waypoints.Count - 2)
                .Select(w => $"{w.Latitude},{w.Longitude}"));
        }

        // Build the request URL
        var url = $"{DirectionsBaseUrl}?origin={origin}&destination={destination}{waypointsParam}" +
                  $"&mode=driving&avoid=highways&key={_apiKey}"; // Avoid expressways

        _logger.LogDebug("Requesting route from Google Maps: origin={Origin}, destination={Destination}, waypoints={WaypointCount}", 
            origin, destination, waypoints.Count - 2);

        _logger.LogDebug("Calling Google Maps API: {Url}", url.Replace(_apiKey!, "***API_KEY***"));
        
        var response = await _httpClient.GetAsync(url);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("❌ HTTP Error from Google Maps: {StatusCode} - {Error}", response.StatusCode, error);
            throw new HttpRequestException($"Google Maps API HTTP error: {response.StatusCode} - {error}");
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        _logger.LogDebug("Google Maps API response (first 500 chars): {Response}", 
            responseBody.Length > 500 ? responseBody.Substring(0, 500) + "..." : responseBody);
        
        var result = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(responseBody);
        
        var status = result.GetProperty("status").GetString();
        if (status != "OK")
        {
            var errorMessage = result.TryGetProperty("error_message", out var errorMsg) 
                ? errorMsg.GetString() 
                : "Unknown error";
            _logger.LogError("❌ Google Maps API Status: {Status}, Error: {ErrorMessage}", status, errorMessage);
            _logger.LogError("Full response: {Response}", responseBody);
            throw new HttpRequestException($"Google Maps API returned status: {status}. Error: {errorMessage}");
        }
        
        _logger.LogInformation("✅ Google Maps API returned status: OK");

        // Extract detailed points from ALL steps (not just overview polyline)
        var routes = result.GetProperty("routes");
        if (routes.GetArrayLength() == 0)
        {
            throw new HttpRequestException("No routes returned from Google Maps API");
        }

        var route = routes[0];
        var allDetailedPoints = new List<(double Latitude, double Longitude)>();

        // Iterate through all legs and steps to get maximum detail
        if (route.TryGetProperty("legs", out var legs))
        {
            foreach (var leg in legs.EnumerateArray())
            {
                if (leg.TryGetProperty("steps", out var steps))
                {
                    foreach (var step in steps.EnumerateArray())
                    {
                        if (step.TryGetProperty("polyline", out var polylineObj))
                        {
                            var encodedPolyline = polylineObj.GetProperty("points").GetString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(encodedPolyline))
                            {
                                var stepPoints = DecodePolyline(encodedPolyline);
                                
                                // Add points, skipping first if we already have points (to avoid duplicates)
                                if (allDetailedPoints.Count > 0)
                                {
                                    allDetailedPoints.AddRange(stepPoints.Skip(1));
                                }
                                else
                                {
                                    allDetailedPoints.AddRange(stepPoints);
                                }
                            }
                        }
                    }
                }
            }
        }

        // Fallback to overview polyline if steps parsing failed
        if (allDetailedPoints.Count == 0)
        {
            _logger.LogWarning("No detailed steps found, using overview polyline");
            var encodedPolyline = route.GetProperty("overview_polyline")
                .GetProperty("points").GetString() ?? string.Empty;
            
            if (!string.IsNullOrEmpty(encodedPolyline))
            {
                allDetailedPoints = DecodePolyline(encodedPolyline);
            }
        }

        if (allDetailedPoints.Count == 0)
        {
            _logger.LogWarning("No points decoded. Using original waypoints.");
            return waypoints;
        }

        _logger.LogInformation("Decoded {Count} detailed points from {Steps} steps", 
            allDetailedPoints.Count, 
            route.TryGetProperty("legs", out var l) ? l.EnumerateArray().SelectMany(leg => 
                leg.TryGetProperty("steps", out var s) ? s.EnumerateArray() : Enumerable.Empty<JsonElement>()
            ).Count() : 0);

        return allDetailedPoints.Select(p => new Coordinate(p.Latitude, p.Longitude)).ToList();
    }

    /// <summary>
    /// Decode Google's encoded polyline format
    /// Reference: https://developers.google.com/maps/documentation/utilities/polylinealgorithm
    /// </summary>
    private List<(double Latitude, double Longitude)> DecodePolyline(string encodedPolyline)
    {
        if (string.IsNullOrEmpty(encodedPolyline))
            return new List<(double, double)>();

        var points = new List<(double, double)>();
        int index = 0;
        int lat = 0, lng = 0;

        while (index < encodedPolyline.Length)
        {
            // Decode latitude
            int result = 0, shift = 0, b;
            do
            {
                b = encodedPolyline[index++] - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            } while (b >= 0x20);
            int dlat = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
            lat += dlat;

            // Decode longitude
            result = 0; 
            shift = 0;
            do
            {
                b = encodedPolyline[index++] - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            } while (b >= 0x20);
            int dlng = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
            lng += dlng;

            points.Add((lat / 1E5, lng / 1E5));
        }

        return points;
    }
}

