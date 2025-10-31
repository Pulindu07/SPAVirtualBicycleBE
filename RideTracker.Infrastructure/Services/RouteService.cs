using Microsoft.EntityFrameworkCore;
using RideTracker.Application.DTOs;
using RideTracker.Application.Interfaces;
using RideTracker.Domain.ValueObjects;
using RideTracker.Infrastructure.Data;

namespace RideTracker.Infrastructure.Services;

public class RouteService : IRouteService
{
    private readonly RideTrackerDbContext _context;
    private const double TOTAL_ROUTE_LENGTH_KM = 1585.0; // Approximate coastal route around Sri Lanka

    public RouteService(RideTrackerDbContext context)
    {
        _context = context;
    }

    public async Task<List<RoutePointDto>> GetRoutePointsAsync()
    {
        var points = await _context.RoutePoints
            .OrderBy(rp => rp.OrderIndex)
            .Select(rp => new RoutePointDto
            {
                Latitude = rp.Latitude,
                Longitude = rp.Longitude
            })
            .ToListAsync();

        return points;
    }

    public Task<double> GetTotalRouteLengthKmAsync()
    {
        return Task.FromResult(TOTAL_ROUTE_LENGTH_KM);
    }

    public async Task<Coordinate> GetCoordinateAtProgressAsync(double progressPercent)
    {
        // Clamp progress between 0 and 100
        progressPercent = Math.Max(0, Math.Min(100, progressPercent));

        var points = await _context.RoutePoints
            .OrderBy(rp => rp.OrderIndex)
            .ToListAsync();

        if (points.Count == 0)
        {
            // Default to Matara if no route points
            return new Coordinate(5.9549, 80.5550);
        }

        // Calculate which segment we're on
        var totalSegments = points.Count - 1;
        var segmentProgress = (progressPercent / 100.0) * totalSegments;
        var segmentIndex = (int)Math.Floor(segmentProgress);
        
        // If we've completed the route, return the last point
        if (segmentIndex >= totalSegments)
        {
            var lastPoint = points[^1];
            return new Coordinate(lastPoint.Latitude, lastPoint.Longitude);
        }

        // Interpolate between two points
        var point1 = points[segmentIndex];
        var point2 = points[segmentIndex + 1];
        var localProgress = segmentProgress - segmentIndex;

        var lat = point1.Latitude + (point2.Latitude - point1.Latitude) * localProgress;
        var lng = point1.Longitude + (point2.Longitude - point1.Longitude) * localProgress;

        return new Coordinate(lat, lng);
    }
}

