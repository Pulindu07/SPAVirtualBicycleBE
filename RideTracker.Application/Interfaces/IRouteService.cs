using RideTracker.Application.DTOs;
using RideTracker.Domain.ValueObjects;

namespace RideTracker.Application.Interfaces;

public interface IRouteService
{
    Task<List<RoutePointDto>> GetRoutePointsAsync(int? routeId = null);
    Task<double> GetTotalRouteLengthKmAsync(int? routeId = null);
    Task<Coordinate> GetCoordinateAtProgressAsync(double progressPercent, int? routeId = null);
    Task<Coordinate> GetCoordinateAtDistanceAsync(double distanceKm, int? routeId = null);
    Task<List<RouteDto>> GetAllRoutesAsync();
    Task<RouteDto?> GetRouteByIdAsync(int routeId);
}

