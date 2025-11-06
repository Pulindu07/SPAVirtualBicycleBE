namespace RideTracker.Application.DTOs;

public class UserDto
{
    public int Id { get; set; }
    public long StravaId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public double TotalDistanceKm { get; set; }
    public long TotalMovingTimeSec { get; set; }
    public DateTime LastSync { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public bool IsSuperAdmin { get; set; }
}

