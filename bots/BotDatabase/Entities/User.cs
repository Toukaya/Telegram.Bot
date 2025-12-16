namespace BotDatabase.Entities;

public class User
{
    public long UserId { get; set; }
    public string Username { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public bool IsBot { get; set; }
    public string LanguageCode { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public List<Message> Messages { get; set; } = new();
    public List<Todo> Todos { get; set; } = new();
    public List<Note> Notes { get; set; } = new();
}
