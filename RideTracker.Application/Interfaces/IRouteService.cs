using RideTracker.Application.DTOs;
using RideTracker.Domain.ValueObjects;

namespace RideTracker.Application.Interfaces;

public interface IRouteService
{
    Task<List<RoutePointDto>> GetRoutePointsAsync();
    Task<double> GetTotalRouteLengthKmAsync();
    Task<Coordinate> GetCoordinateAtProgressAsync(double progressPercent);
    Task<Coordinate> GetCoordinateAtDistanceAsync(double distanceKm);
}

