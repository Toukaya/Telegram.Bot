namespace TelegramBotService.TaskAI;

public class TaskAiConfig
{
    public bool Enabled { get; set; } = true;
    public string Model { get; set; } = "gpt-4o-mini";
}
