namespace BotDatabase.Entities;

public class Todo
{
    public int Id { get; set; }
    public long UserId { get; set; }
    public long ChatId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = TodoStatus.Pending;
    public string Priority { get; set; } = TodoPriority.Normal;
    public DateTime DueAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User User { get; set; }
}

public static class TodoStatus
{
    public const string Pending = "pending";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
}

public static class TodoPriority
{
    public const string Low = "low";
    public const string Normal = "normal";
    public const string High = "high";
    public const string Urgent = "urgent";
}
