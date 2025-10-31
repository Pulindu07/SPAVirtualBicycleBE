namespace RideTracker.Domain.Entities;

public class RoutePoint
{
    public int Id { get; set; }
    public int OrderIndex { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

