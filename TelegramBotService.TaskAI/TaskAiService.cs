using System.Text.RegularExpressions;
using TelegramBotService.TaskAI.Models;
using TelegramBotService.AI;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TelegramBotService.TaskAI;

public class TaskAiService : ITaskAiService
{
    private readonly IAiService _aiService;
    private readonly TaskAiConfig _config;
    private readonly IDeserializer _yamlDeserializer;
    private readonly ISerializer _yamlSerializer;

    public bool IsAvailable => _aiService != null && _aiService.IsAvailable;

    public TaskAiService(IAiService aiService, TaskAiConfig config = null)
    {
        _aiService = aiService;
        _config = config ?? new TaskAiConfig();

        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    public async Task<TaskAiResult<Backlog>> GenerateBacklogAsync(
        string specification,
        CancellationToken ct = default)
    {
        if (!IsAvailable)
        {
            return TaskAiResult<Backlog>.Fail("AI service is not available");
        }

        try
        {
            var messages = new List<AiChatMessage>
            {
                AiChatMessage.System(TaskAiPrompts.SystemPrompt),
                AiChatMessage.User(specification)
            };

            var options = new AiCompletionOptions
            {
                Model = _config.Model,
                Temperature = 0.7f,
                MaxTokens = 2048
            };

            var result = await _aiService.ChatAsync(messages, options, ct);

            if (!result.Success)
            {
                return TaskAiResult<Backlog>.Fail($"LLM call failed: {result.Error}");
            }

            var yaml = ExtractYamlContent(result.Text);
            var parseResult = ParseBacklog(yaml);

            if (!parseResult.Success)
            {
                return TaskAiResult<Backlog>.Fail($"Failed to parse backlog: {parseResult.Error}");
            }

            var validation = parseResult.Data.Validate();
            if (!validation.IsValid)
            {
                return TaskAiResult<Backlog>.Fail(
                    $"Backlog validation failed: {string.Join("; ", validation.Errors)}"
                );
            }

            return parseResult;
        }
        catch (Exception ex)
        {
            return TaskAiResult<Backlog>.Fail($"Error: {ex.Message}");
        }
    }

    public TaskAiResult<Backlog> ParseBacklog(string yaml)
    {
        try
        {
            var backlog = _yamlDeserializer.Deserialize<Backlog>(yaml);
            if (backlog == null)
            {
                return TaskAiResult<Backlog>.Fail("Failed to deserialize YAML");
            }
            return TaskAiResult<Backlog>.Ok(backlog);
        }
        catch (Exception ex)
        {
            return TaskAiResult<Backlog>.Fail($"YAML parse error: {ex.Message}");
        }
    }

    public string SerializeBacklog(Backlog backlog)
    {
        return _yamlSerializer.Serialize(backlog);
    }

    private string ExtractYamlContent(string text)
    {
        // Try to extract YAML from markdown code blocks
        var codeBlockPattern = new Regex(@"```(?:yaml)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase);
        var match = codeBlockPattern.Match(text);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        // If no code block, return the text as-is (might be raw YAML)
        return text.Trim();
    }
}
