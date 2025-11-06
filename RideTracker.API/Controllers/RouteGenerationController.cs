using Microsoft.AspNetCore.Mvc;
using RideTracker.Domain.ValueObjects;
using RideTracker.Infrastructure.Services;

namespace RideTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RouteGenerationController : ControllerBase
{
    private readonly RouteGenerationService _routeGenerationService;
    private readonly ILogger<RouteGenerationController> _logger;

    public RouteGenerationController(
        RouteGenerationService routeGenerationService,
        ILogger<RouteGenerationController> logger)
    {
        _routeGenerationService = routeGenerationService;
        _logger = logger;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateRoute([FromBody] GenerateRouteRequest? request = null)
    {
        try
        {
            _logger.LogInformation("Starting route generation...");
            
            int? routeId = request?.RouteId;
            string? routeName = request?.RouteName;
            string? routeDescription = request?.RouteDescription;
            List<Coordinate>? waypoints = null;

            if (request?.Waypoints != null && request.Waypoints.Any())
            {
                waypoints = request.Waypoints.Select(w => new Coordinate(w.Latitude, w.Longitude)).ToList();
            }
            
            var result = await _routeGenerationService.GenerateAndSaveRouteAsync(
                routeId, 
                routeName, 
                routeDescription, 
                waypoints
            );
            
            return Ok(new 
            { 
                message = "Route generated successfully with road data",
                routeId = result.RouteId,
                routeName = result.RouteName,
                totalDistanceKm = result.TotalDistanceKm,
                pointCount = result.PointCount,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate route");
            return StatusCode(500, new 
            { 
                message = "Failed to generate route", 
                error = ex.Message 
            });
        }
    }
}

// Request/Response models
public class GenerateRouteRequest
{
    public int? RouteId { get; set; } // Optional: if provided, updates existing route
    public string? RouteName { get; set; }
    public string? RouteDescription { get; set; }
    public List<WaypointDto>? Waypoints { get; set; } // Optional: if provided, uses custom waypoints
}

public class WaypointDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

