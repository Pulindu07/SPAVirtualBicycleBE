using Microsoft.Extensions.Logging;
using RideTracker.Application.Interfaces;
using RideTracker.Domain.Entities;
using RideTracker.Domain.ValueObjects;

namespace RideTracker.Infrastructure.Services;

public class RouteGenerationService
{
    private readonly IRouteGenerationService _openRouteService;
    private readonly IRepository<RoutePoint> _routeRepository;
    private readonly ILogger<RouteGenerationService> _logger;

    public RouteGenerationService(
        IRouteGenerationService openRouteService,
        IRepository<RoutePoint> routeRepository,
        ILogger<RouteGenerationService> logger)
    {
        _openRouteService = openRouteService;
        _routeRepository = routeRepository;
        _logger = logger;
    }

    public async Task GenerateAndSaveRouteAsync()
    {
        _logger.LogInformation("Starting route generation with OpenRouteService...");

        // Get base waypoints (major cities around Sri Lanka coast)
        var baseWaypoints = GetBaseWaypoints();

        _logger.LogInformation("Base waypoints: {Count}", baseWaypoints.Count);

        // Generate detailed road route
        var detailedRoute = await _openRouteService.GenerateRoadRouteAsync(baseWaypoints);

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
        // Dense waypoints along Sri Lanka's coastal roads to force coastal routing
        // More waypoints = route stays closer to coast instead of cutting inland
        return new List<Coordinate>
        {
            // Start: Matara (South Coast) - A2 Highway
            new Coordinate(5.9470, 80.5490),  // Matara
            new Coordinate(5.9620, 80.4850),  // Polhena
            new Coordinate(5.9750, 80.3920),  // Ahangama
            new Coordinate(6.0100, 80.3180),  // Koggala
            new Coordinate(6.0535, 80.2210),  // Galle Fort
            
            // Galle to Colombo - A2 Coastal Highway (South Coast)
            new Coordinate(6.0880, 80.1520),  // Unawatuna area
            new Coordinate(6.1350, 80.0990),  // Hikkaduwa
            new Coordinate(6.2090, 80.0530),  // Dodanduwa
            new Coordinate(6.2760, 80.0180),  // Ambalangoda
            new Coordinate(6.3480, 79.9850),  // Balapitiya
            new Coordinate(6.4230, 79.9710),  // Bentota
            new Coordinate(6.4890, 79.9620),  // Aluthgama
            new Coordinate(6.5617, 79.9580),  // Kalutara North
            new Coordinate(6.5850, 79.9610),  // Kalutara South
            new Coordinate(6.6490, 79.9750),  // Wadduwa
            new Coordinate(6.7082, 79.9896),  // Panadura
            new Coordinate(6.7680, 79.9980),  // Moratuwa
            new Coordinate(6.8350, 79.9620),  // Dehiwala
            new Coordinate(6.8710, 79.8610),  // Mount Lavinia
            new Coordinate(6.9319, 79.8538),  // Colombo (Galle Face)
            
            // Colombo to Chilaw - A3 Coastal Road (West Coast)
            new Coordinate(6.9580, 79.8540),  // Colombo Port area
            new Coordinate(7.0310, 79.8750),  // Ja-Ela
            new Coordinate(7.0873, 79.8840),  // Negombo
            new Coordinate(7.1520, 79.8630),  // Marawila
            new Coordinate(7.2140, 79.8470),  // Madampe
            new Coordinate(7.2820, 79.8350),  // Wennappuwa
            new Coordinate(7.3662, 79.8324),  // Chilaw
            
            // Chilaw to Puttalam - A3 continues
            new Coordinate(7.4520, 79.8280),  // Bangadeniya
            new Coordinate(7.5680, 79.8290),  // Mundel
            new Coordinate(7.6850, 79.8310),  // Kalpitiya Road Junction
            new Coordinate(7.9218, 79.8391),  // Puttalam
            
            // Puttalam to Mannar - Via A12 highway (avoiding problematic peninsula coordinates)
            new Coordinate(8.1420, 79.8350),  // North of Puttalam on A12
            new Coordinate(8.3650, 79.8520),  // Continue A12 towards Mannar
            new Coordinate(8.5811, 79.9043),  // Mannar Island
            
            // Mannar to Jaffna - A14 Highway (fewer waypoints to reduce API calls)
            new Coordinate(8.7580, 79.9850),  // North of Mannar
            new Coordinate(9.0180, 80.0920),  // Approach to Elephant Pass
            new Coordinate(9.1680, 80.1250),  // Elephant Pass
            new Coordinate(9.3850, 80.0820),  // Chavakachcheri  
            new Coordinate(9.5350, 80.0480),  // Chunnakam
            new Coordinate(9.6615, 80.0255),  // Jaffna City
            
            // Jaffna to Mullaitivu - A9 South then coastal road (simplified)
            new Coordinate(9.6350, 80.1850),  // East Jaffna (on A9)
            new Coordinate(9.5420, 80.4180),  // Towards Point Pedro area
            new Coordinate(9.4120, 80.6520),  // Continue south
            new Coordinate(9.2680, 80.8140),  // Mullaitivu
            
            // Mullaitivu to Trincomalee - A15 Coastal Highway (East Coast)
            new Coordinate(9.0520, 80.9420),  // South of Mullaitivu
            new Coordinate(8.8720, 81.0650),  // Pulmoddai area
            new Coordinate(8.7180, 81.1650),  // Kuchchaveli
            new Coordinate(8.5691, 81.2335),  // Trincomalee
            
            // Trincomalee to Batticaloa - A15 continues
            new Coordinate(8.4520, 81.1950),  // Kinniya
            new Coordinate(8.2850, 81.1620),  // Mutur area
            new Coordinate(8.0420, 81.2180),  // Seruwila area
            new Coordinate(7.8650, 81.4520),  // Padiyatalawa
            new Coordinate(7.7167, 81.6854),  // Batticaloa
            
            // Batticaloa to Arugam Bay - A4 Coastal Road (Southeast Coast)
            new Coordinate(7.5820, 81.7120),  // Kalmunai
            new Coordinate(7.3850, 81.7650),  // Akkaraipattu
            new Coordinate(7.1620, 81.8350),  // Pottuvil
            new Coordinate(6.8420, 81.8370),  // Arugam Bay
            
            // Arugam Bay to Hambantota - A4 continues (going west along south coast)
            new Coordinate(6.6820, 81.7420),  // Panama
            new Coordinate(6.5120, 81.5850),  // Okanda
            new Coordinate(6.4240, 81.4120),  // Near Yala
            new Coordinate(6.2850, 81.2850),  // Kirinda
            new Coordinate(6.1244, 81.1196),  // Hambantota
            
            // Hambantota to Matara - A2 Coastal Highway (completing the loop)
            new Coordinate(6.0820, 81.0320),  // Ambalantota
            new Coordinate(6.0380, 80.9420),  // Weeraketiya area
            new Coordinate(6.0071, 80.8817),  // Tangalle
            new Coordinate(5.9920, 80.8020),  // Rekawa
            new Coordinate(5.9763, 80.7091),  // Dikwella
            new Coordinate(5.9620, 80.6120),  // Beliatta
            new Coordinate(5.9470, 80.5490),  // Back to Matara (complete coastal loop!)
        };
    }
}

