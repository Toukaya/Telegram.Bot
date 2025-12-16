namespace BotDatabase.Entities;

public class Chat
{
    public long ChatId { get; set; }
    public string ChatType { get; set; } = "";   // private, group, supergroup, channel
    public string Title { get; set; } = "";
    public string Username { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public List<Message> Messages { get; set; } = new();
}

public static class ChatTypes
{
    public const string Private = "private";
    public const string Group = "group";
    public const string Supergroup = "supergroup";
    public const string Channel = "channel";
}
