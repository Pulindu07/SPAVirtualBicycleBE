using RideTracker.Domain.ValueObjects;

namespace RideTracker.Application.Interfaces;

public interface IRouteGenerationService
{
    Task<List<Coordinate>> GenerateRoadRouteAsync(List<Coordinate> waypoints);
}

