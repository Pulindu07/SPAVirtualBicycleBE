namespace RideTracker.Domain.Entities;

public class Route
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double TotalDistanceKm { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<RoutePoint> RoutePoints { get; set; } = new List<RoutePoint>();
}

