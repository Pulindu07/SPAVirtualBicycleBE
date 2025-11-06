using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RideTracker.Application.Interfaces;
using RideTracker.Domain.Entities;
using RideTracker.Domain.ValueObjects;
using RideTracker.Infrastructure.Data;

namespace RideTracker.Infrastructure.Services;

public class RouteGenerationService
{
    private readonly IRouteGenerationService _routeGenerationService;
    private readonly IRepository<RoutePoint> _routeRepository;
    private readonly IRepository<Route> _routeEntityRepository;
    private readonly RideTrackerDbContext _context;
    private readonly ILogger<RouteGenerationService> _logger;

    public RouteGenerationService(
        IRouteGenerationService routeGenerationService,
        IRepository<RoutePoint> routeRepository,
        IRepository<Route> routeEntityRepository,
        RideTrackerDbContext context,
        ILogger<RouteGenerationService> logger)
    {
        _routeGenerationService = routeGenerationService;
        _routeRepository = routeRepository;
        _routeEntityRepository = routeEntityRepository;
        _context = context;
        _logger = logger;
    }

    public async Task<RouteGenerationResult> GenerateAndSaveRouteAsync(
        int? routeId = null, 
        string? routeName = null, 
        string? routeDescription = null,
        List<Coordinate>? customWaypoints = null)
    {
        _logger.LogInformation("Starting route generation with configured route service...");

        // Use custom waypoints if provided, otherwise use default waypoints
        var baseWaypoints = customWaypoints ?? GetBaseWaypoints();

        _logger.LogInformation("Using {Count} base waypoints for route generation", baseWaypoints.Count);

        // Generate detailed road route using the injected service (GoogleMapsRouteService or OpenRouteService)
        _logger.LogInformation("Route service type: {ServiceType}", _routeGenerationService.GetType().Name);
        var detailedRoute = await _routeGenerationService.GenerateRoadRouteAsync(baseWaypoints);

        _logger.LogInformation("Detailed route generated with {Count} points", detailedRoute.Count);

        // Calculate total distance
        double totalDistance = 0;
        for (int i = 0; i < detailedRoute.Count - 1; i++)
        {
            totalDistance += CalculateDistance(
                detailedRoute[i].Latitude, detailedRoute[i].Longitude,
                detailedRoute[i + 1].Latitude, detailedRoute[i + 1].Longitude
            );
        }

        Route route;
        if (routeId.HasValue)
        {
            // Update existing route
            route = await _context.Routes.FindAsync(routeId.Value);
            if (route == null)
                throw new InvalidOperationException($"Route with ID {routeId.Value} not found");
            
            if (!string.IsNullOrWhiteSpace(routeName))
                route.Name = routeName;
            if (!string.IsNullOrWhiteSpace(routeDescription))
                route.Description = routeDescription;
            route.TotalDistanceKm = totalDistance;
            
            // Clear existing route points for this route
            var existingPoints = await _context.RoutePoints
                .Where(rp => rp.RouteId == routeId.Value)
                .ToListAsync();
            _context.RoutePoints.RemoveRange(existingPoints);
        }
        else
        {
            // Create new route
            route = new Route
            {
                Name = routeName ?? "Generated Route",
                Description = routeDescription,
                TotalDistanceKm = totalDistance,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            await _routeEntityRepository.AddAsync(route);
            await _routeEntityRepository.SaveChangesAsync();
        }

        // Save new route points
        var routePoints = detailedRoute.Select((coord, index) => new RoutePoint
        {
            RouteId = route.Id,
            OrderIndex = index,
            Latitude = coord.Latitude,
            Longitude = coord.Longitude
        }).ToList();

        await _routeRepository.AddRangeAsync(routePoints);
        await _routeRepository.SaveChangesAsync();

        _logger.LogInformation("Route saved successfully with {Count} points! Route ID: {RouteId}", routePoints.Count, route.Id);

        return new RouteGenerationResult
        {
            RouteId = route.Id,
            RouteName = route.Name,
            TotalDistanceKm = route.TotalDistanceKm,
            PointCount = routePoints.Count
        };
    }

    // Haversine formula to calculate distance between two lat/lng points in kilometers
    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Earth's radius in kilometers
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    public class RouteGenerationResult
    {
        public int RouteId { get; set; }
        public string RouteName { get; set; } = string.Empty;
        public double TotalDistanceKm { get; set; }
        public int PointCount { get; set; }
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

