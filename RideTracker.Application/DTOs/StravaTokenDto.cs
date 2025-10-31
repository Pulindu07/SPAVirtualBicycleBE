namespace RideTracker.Application.DTOs;

public class StravaTokenDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public DateTime ExpiresAt { get; set; }
    public StravaAthleteDto? Athlete { get; set; }
}

public class StravaAthleteDto
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Firstname { get; set; } = string.Empty;
    public string Lastname { get; set; } = string.Empty;
}

