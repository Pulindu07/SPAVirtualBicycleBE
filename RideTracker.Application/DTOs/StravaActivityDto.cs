namespace RideTracker.Application.DTOs;

public class StravaActivityDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Distance { get; set; } // in meters
    public long MovingTime { get; set; } // in seconds
    public DateTime StartDate { get; set; }
    public double AverageSpeed { get; set; }
}

