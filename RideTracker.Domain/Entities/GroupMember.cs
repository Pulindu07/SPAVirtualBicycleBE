namespace RideTracker.Domain.Entities;

public class GroupMember
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public int UserId { get; set; }
    public string Role { get; set; } = "member"; // "admin" or "member"
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Group Group { get; set; } = null!;
    public User User { get; set; } = null!;
}

