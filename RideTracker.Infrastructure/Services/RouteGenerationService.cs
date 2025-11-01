using Microsoft.Extensions.Logging;
using RideTracker.Application.Interfaces;
using RideTracker.Domain.Entities;
using RideTracker.Domain.ValueObjects;

namespace RideTracker.Infrastructure.Services;

public class RouteGenerationService
{
    private readonly IRouteGenerationService _routeGenerationService;
    private readonly IRepository<RoutePoint> _routeRepository;
    private readonly ILogger<RouteGenerationService> _logger;

    public RouteGenerationService(
        IRouteGenerationService routeGenerationService,
        IRepository<RoutePoint> routeRepository,
        ILogger<RouteGenerationService> logger)
    {
        _routeGenerationService = routeGenerationService;
        _routeRepository = routeRepository;
        _logger = logger;
    }

    public async Task GenerateAndSaveRouteAsync()
    {
        _logger.LogInformation("Starting route generation with configured route service...");

        // Get base waypoints (major cities around Sri Lanka coast)
        var baseWaypoints = GetBaseWaypoints();

        _logger.LogInformation("Using {Count} base waypoints for route generation", baseWaypoints.Count);

        // Generate detailed road route using the injected service (GoogleMapsRouteService or OpenRouteService)
        _logger.LogInformation("Route service type: {ServiceType}", _routeGenerationService.GetType().Name);
        var detailedRoute = await _routeGenerationService.GenerateRoadRouteAsync(baseWaypoints);

        _logger.LogInformation("Detailed route generated with {Count} points", detailedRoute.Count);

        // Clear existing route points
        var existingPoints = await _routeRepository.GetAllAsync();
        foreach (var point in existingPoints)
        {
            await _routeRepository.DeleteAsync(point);
        }
        await _routeRepository.SaveChangesAsync();

        // Save new route points
        var routePoints = detailedRoute.Select((coord, index) => new RoutePoint
        {
            OrderIndex = index,
            Latitude = coord.Latitude,
            Longitude = coord.Longitude
        }).ToList();

        await _routeRepository.AddRangeAsync(routePoints);
        await _routeRepository.SaveChangesAsync();

        _logger.LogInformation("Route saved successfully with {Count} points!", routePoints.Count);
    }

    private List<Coordinate> GetBaseWaypoints()
    {
        // Route 1: Dondra Head (Southernmost tip) to Point Pedro (Northernmost tip)
        return new List<Coordinate>
        {
            new Coordinate(5.9244, 80.5877),  // 1. Dondra (START - Southernmost Tip)
            new Coordinate(6.0535, 80.2210),  // 2. Galle
            new Coordinate(6.9271, 79.8612),  // 3. Colombo
            new Coordinate(7.0873, 79.8840),  // 4. Negombo
            new Coordinate(7.9218, 79.8391),  // 5. Puttalam
            new Coordinate(8.435, 79.889),    // 6. Marichchukkaddi (Forces B403 coastal road)
            new Coordinate(8.5811, 79.9043),  // 7. Mannar
            new Coordinate(9.6615, 80.0255),  // 8. Jaffna
            new Coordinate(9.8255, 80.2335)   // 9. Point Pedro (FINISH - Northernmost Tip)
        };
    }
}

