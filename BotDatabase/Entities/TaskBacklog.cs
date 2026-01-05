namespace BotDatabase.Entities;

public class TaskBacklog
{
    public int Id { get; set; }
    public long ChatId { get; set; }
    public string Project { get; set; } = "";
    public string BacklogYaml { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
