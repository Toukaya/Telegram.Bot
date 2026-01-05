using TelegramBotService.TaskAI.Models;

namespace TelegramBotService.TaskAI;

public interface ITaskAiService
{
    bool IsAvailable { get; }

    // Generate a task backlog from natural language specification
    Task<TaskAiResult<Backlog>> GenerateBacklogAsync(
        string specification,
        CancellationToken ct = default);

    // Parse YAML content into a Backlog
    TaskAiResult<Backlog> ParseBacklog(string yaml);

    // Serialize a Backlog to YAML
    string SerializeBacklog(Backlog backlog);
}
