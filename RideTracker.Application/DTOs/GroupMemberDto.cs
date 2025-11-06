namespace RideTracker.Application.DTOs;

public class GroupMemberDto
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public double TotalDistanceKm { get; set; }
}

public class AddGroupMemberDto
{
    public int UserId { get; set; }
    public string Role { get; set; } = "member";
}

