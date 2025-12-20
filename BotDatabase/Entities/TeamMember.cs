namespace BotDatabase.Entities;

public class TeamMember
{
    public int Id { get; set; }
    public long TelegramUserId { get; set; }
    public string Name { get; set; } = "";
    public string Alias { get; set; } = "";
    public string Role { get; set; } = TeamMemberRole.Developer;
    public string Status { get; set; } = TeamMemberStatus.Active;
    public DateTime JoinedAt { get; set; }
    public DateTime LeftAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public List<WeeklyReport> WeeklyReports { get; set; } = new();
}

public static class TeamMemberRole
{
    public const string Developer = "developer";
    public const string Designer = "designer";
    public const string Manager = "manager";
}

public static class TeamMemberStatus
{
    public const string Active = "active";
    public const string Vacation = "vacation";
    public const string Left = "left";
}
