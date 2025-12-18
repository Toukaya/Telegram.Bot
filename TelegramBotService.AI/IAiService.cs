namespace TelegramBotService.AI;

// Result of AI completion
public class AiCompletionResult
{
    public bool Success { get; set; }
    public string Text { get; set; } = "";
    public string Error { get; set; } = "";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public string Model { get; set; } = "";
    public double DurationMs { get; set; }

    public static AiCompletionResult Ok(string text, string model = "", int promptTokens = 0, int completionTokens = 0)
    {
        return new AiCompletionResult
        {
            Success = true,
            Text = text,
            Model = model,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = promptTokens + completionTokens
        };
    }

    public static AiCompletionResult Fail(string error)
    {
        return new AiCompletionResult
        {
            Success = false,
            Error = error
        };
    }

    public static AiCompletionResult Unavailable(string reason = "AI service not available")
    {
        return new AiCompletionResult
        {
            Success = false,
            Error = reason
        };
    }
}

// Options for AI completion
public class AiCompletionOptions
{
    public string Model { get; set; } = "";           // Override default model
    public float Temperature { get; set; } = 0.7f;    // 0.0-2.0, lower = more deterministic
    public int MaxTokens { get; set; } = 2048;        // Max tokens in response
    public string SystemPrompt { get; set; } = "";    // System/instruction prompt
    public bool Stream { get; set; } = false;         // Enable streaming (not implemented yet)
}

// Chat message for multi-turn conversations
public class AiChatMessage
{
    public string Role { get; set; } = "user";        // system, user, assistant
    public string Content { get; set; } = "";

    public static AiChatMessage System(string content) => new() { Role = "system", Content = content };
    public static AiChatMessage User(string content) => new() { Role = "user", Content = content };
    public static AiChatMessage Assistant(string content) => new() { Role = "assistant", Content = content };
}

// Interface for AI service
public interface IAiService
{
    // Service name
    string Name { get; }

    // Check if service is available
    bool IsAvailable { get; }

    // Default model
    string DefaultModel { get; }

    // List available models
    Task<List<string>> ListModelsAsync(CancellationToken ct = default);

    // Simple text completion
    Task<AiCompletionResult> CompleteAsync(
        string prompt,
        AiCompletionOptions options = null,
        CancellationToken ct = default);

    // Chat completion with message history
    Task<AiCompletionResult> ChatAsync(
        List<AiChatMessage> messages,
        AiCompletionOptions options = null,
        CancellationToken ct = default);

    // Summarize text
    Task<AiCompletionResult> SummarizeAsync(
        string text,
        string instruction = null,
        AiCompletionOptions options = null,
        CancellationToken ct = default);

    // Answer question based on context
    Task<AiCompletionResult> AnswerAsync(
        string question,
        string context,
        AiCompletionOptions options = null,
        CancellationToken ct = default);
}
