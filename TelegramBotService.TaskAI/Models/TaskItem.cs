using YamlDotNet.Serialization;

namespace TelegramBotService.TaskAI.Models;

public enum TaskState
{
    Todo,
    Done
}

public class TaskItem
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = "";

    [YamlMember(Alias = "title")]
    public string Title { get; set; } = "";

    [YamlMember(Alias = "depends")]
    public List<string> Dependencies { get; set; } = new();

    [YamlMember(Alias = "state")]
    public TaskState State { get; set; } = TaskState.Todo;

    [YamlMember(Alias = "description")]
    public string Description { get; set; } = "";

    [YamlMember(Alias = "deliverable")]
    public object Deliverable { get; set; } = "";

    [YamlMember(Alias = "done_when")]
    public object DoneWhen { get; set; } = "";

    public List<string> GetDeliverables()
    {
        if (Deliverable is List<object> list)
        {
            return list.Select(x => x.ToString()).ToList();
        }
        if (Deliverable is string s && !string.IsNullOrEmpty(s))
        {
            return new List<string> { s };
        }
        return new List<string>();
    }

    public List<string> GetDoneWhenCriteria()
    {
        if (DoneWhen is List<object> list)
        {
            return list.Select(x => x.ToString()).ToList();
        }
        if (DoneWhen is string s && !string.IsNullOrEmpty(s))
        {
            return new List<string> { s };
        }
        return new List<string>();
    }
}
