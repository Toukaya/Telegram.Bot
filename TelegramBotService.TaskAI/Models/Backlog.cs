using YamlDotNet.Serialization;

namespace TelegramBotService.TaskAI.Models;

public class Backlog
{
    [YamlMember(Alias = "project")]
    public string Project { get; set; } = "";

    [YamlMember(Alias = "rust_version")]
    public string RustVersion { get; set; } = "";

    [YamlMember(Alias = "success_criteria")]
    public List<string> SuccessCriteria { get; set; } = new();

    [YamlMember(Alias = "environment")]
    public Dictionary<string, string> Environment { get; set; } = new();

    [YamlMember(Alias = "epics")]
    public List<Epic> Epics { get; set; } = new();

    [YamlMember(Alias = "tasks")]
    public List<TaskItem> Tasks { get; set; } = new();

    public List<TaskItem> GetAllTasks()
    {
        var allTasks = new List<TaskItem>();
        allTasks.AddRange(Tasks);
        foreach (var epic in Epics)
        {
            allTasks.AddRange(epic.Tasks);
        }
        return allTasks;
    }

    public List<TaskItem> GetReadyTasks()
    {
        var allTasks = GetAllTasks();
        var doneTasks = new HashSet<string>(
            allTasks.Where(t => t.State == TaskState.Done).Select(t => t.Id)
        );

        return allTasks
            .Where(t => t.State == TaskState.Todo)
            .Where(t => t.Dependencies.All(dep => doneTasks.Contains(dep)))
            .ToList();
    }

    public BacklogValidationResult Validate()
    {
        var errors = new List<string>();
        var allTasks = GetAllTasks();
        var taskIds = new HashSet<string>(allTasks.Select(t => t.Id));

        // Check for duplicate IDs
        var duplicates = allTasks
            .GroupBy(t => t.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var dup in duplicates)
        {
            errors.Add($"Duplicate task ID: {dup}");
        }

        // Check for missing dependencies
        foreach (var task in allTasks)
        {
            foreach (var dep in task.Dependencies)
            {
                if (!taskIds.Contains(dep))
                {
                    errors.Add($"Task '{task.Id}' has missing dependency: {dep}");
                }
            }
        }

        // Check for cycles
        var cycleError = CheckCycles(allTasks);
        if (cycleError != null)
        {
            errors.Add(cycleError);
        }

        return new BacklogValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    private string CheckCycles(List<TaskItem> tasks)
    {
        var taskMap = tasks.ToDictionary(t => t.Id, t => t);
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var task in tasks)
        {
            if (HasCycle(task.Id, taskMap, visited, recursionStack, out var cycle))
            {
                return $"Dependency cycle detected: {string.Join(" -> ", cycle)}";
            }
        }

        return null;
    }

    private bool HasCycle(
        string taskId,
        Dictionary<string, TaskItem> taskMap,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        out List<string> cycle)
    {
        cycle = new List<string>();

        if (recursionStack.Contains(taskId))
        {
            cycle.Add(taskId);
            return true;
        }

        if (visited.Contains(taskId))
        {
            return false;
        }

        visited.Add(taskId);
        recursionStack.Add(taskId);

        if (taskMap.TryGetValue(taskId, out var task))
        {
            foreach (var dep in task.Dependencies)
            {
                if (HasCycle(dep, taskMap, visited, recursionStack, out cycle))
                {
                    cycle.Insert(0, taskId);
                    return true;
                }
            }
        }

        recursionStack.Remove(taskId);
        return false;
    }

    public bool MarkTaskDone(string taskId)
    {
        var task = GetAllTasks().FirstOrDefault(t => t.Id == taskId);
        if (task == null)
        {
            return false;
        }

        task.State = TaskState.Done;
        return true;
    }
}

public class BacklogValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}
