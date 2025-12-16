namespace BotDatabase.Entities;

public class Note
{
    public int Id { get; set; }
    public long UserId { get; set; }
    public long ChatId { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string Tags { get; set; } = "";         // comma-separated tags
    public bool IsPinned { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User User { get; set; }
}
