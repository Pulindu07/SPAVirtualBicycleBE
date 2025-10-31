namespace RideTracker.Application.DTOs;

public class UserProgressDto
{
    public double TotalDistanceKm { get; set; }
    public long TotalMovingTimeSec { get; set; }
    public double ProgressPercent { get; set; }
    public double CurrentLat { get; set; }
    public double CurrentLng { get; set; }
    public DateTime LastSync { get; set; }
    public string Username { get; set; } = string.Empty;
}

