namespace RideTracker.Application.DTOs;

public class GroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public int CreatedByUserId { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int MemberCount { get; set; }
    public List<GroupMemberDto> Members { get; set; } = new();
}

public class CreateGroupDto
{
    public string Name { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
}

public class UpdateGroupDto
{
    public string? Name { get; set; }
    public string? IconUrl { get; set; }
}

