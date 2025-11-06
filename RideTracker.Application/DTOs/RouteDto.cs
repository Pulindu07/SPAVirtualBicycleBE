namespace RideTracker.Application.DTOs;

public class RouteDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double TotalDistanceKm { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

