using Microsoft.EntityFrameworkCore;
using RideTracker.Application.DTOs;
using RideTracker.Application.Interfaces;
using RideTracker.Domain.ValueObjects;
using RideTracker.Infrastructure.Data;

namespace RideTracker.Infrastructure.Services;

public class RouteService : IRouteService
{
    private readonly RideTrackerDbContext _context;

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

    public async Task<double> GetTotalRouteLengthKmAsync()
    {
        var points = await _context.RoutePoints
            .OrderBy(rp => rp.OrderIndex)
            .ToListAsync();

        if (points.Count < 2)
        {
            return 1585.0; // Fallback to approximate value
        }

        double totalDistance = 0;
        for (int i = 0; i < points.Count - 1; i++)
        {
            totalDistance += CalculateDistance(
                points[i].Latitude, points[i].Longitude,
                points[i + 1].Latitude, points[i + 1].Longitude
            );
        }

        return totalDistance;
    }

    public async Task<Coordinate> GetCoordinateAtProgressAsync(double progressPercent)
    {
        // Convert percentage to actual distance
        var totalRouteLength = await GetTotalRouteLengthKmAsync();
        var targetDistance = (progressPercent / 100.0) * totalRouteLength;
        
        return await GetCoordinateAtDistanceAsync(targetDistance);
    }

    public async Task<Coordinate> GetCoordinateAtDistanceAsync(double distanceKm)
    {
        var points = await _context.RoutePoints
            .OrderBy(rp => rp.OrderIndex)
            .ToListAsync();

        if (points.Count == 0)
        {
            // Default to Matara if no route points
            return new Coordinate(5.9549, 80.5550);
        }

        if (points.Count == 1)
        {
            return new Coordinate(points[0].Latitude, points[0].Longitude);
        }

        // Calculate cumulative distances for each waypoint
        var cumulativeDistances = new List<double> { 0 }; // First point is at 0km
        double totalDistance = 0;

        for (int i = 0; i < points.Count - 1; i++)
        {
            var distance = CalculateDistance(
                points[i].Latitude, points[i].Longitude,
                points[i + 1].Latitude, points[i + 1].Longitude
            );
            totalDistance += distance;
            cumulativeDistances.Add(totalDistance);
        }

        // Clamp distance to route bounds (handle cases where user exceeds total distance)
        var targetDistance = Math.Max(0, Math.Min(distanceKm, totalDistance));

        // Handle edge case: if at or past the end, return last point
        if (targetDistance >= totalDistance)
        {
            var lastPoint = points[^1];
            return new Coordinate(lastPoint.Latitude, lastPoint.Longitude);
        }

        // Find which segment the target distance falls in
        int segmentIndex = 0;
        for (int i = 0; i < cumulativeDistances.Count - 1; i++)
        {
            if (targetDistance >= cumulativeDistances[i] && targetDistance <= cumulativeDistances[i + 1])
            {
                segmentIndex = i;
                break;
            }
        }

        // Interpolate within the segment based on actual distance
        var point1 = points[segmentIndex];
        var point2 = points[segmentIndex + 1];
        var segmentStartDist = cumulativeDistances[segmentIndex];
        var segmentEndDist = cumulativeDistances[segmentIndex + 1];
        var segmentLength = segmentEndDist - segmentStartDist;
        
        // Calculate how far along this segment we are (0.0 to 1.0)
        var localProgress = segmentLength > 0 
            ? (targetDistance - segmentStartDist) / segmentLength 
            : 0;

        // Interpolate latitude and longitude
        var lat = point1.Latitude + (point2.Latitude - point1.Latitude) * localProgress;
        var lng = point1.Longitude + (point2.Longitude - point1.Longitude) * localProgress;

        return new Coordinate(lat, lng);
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
}

