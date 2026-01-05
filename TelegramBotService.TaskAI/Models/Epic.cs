using YamlDotNet.Serialization;

namespace TelegramBotService.TaskAI.Models;

public class Epic
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = "";

    [YamlMember(Alias = "title")]
    public string Title { get; set; } = "";

    [YamlMember(Alias = "tasks")]
    public List<TaskItem> Tasks { get; set; } = new();
}
