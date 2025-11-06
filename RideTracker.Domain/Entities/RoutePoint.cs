namespace RideTracker.Domain.Entities;

public class RoutePoint
{
    public int Id { get; set; }
    public int RouteId { get; set; }
    public int OrderIndex { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    // Navigation properties
    public Route Route { get; set; } = null!;
}

