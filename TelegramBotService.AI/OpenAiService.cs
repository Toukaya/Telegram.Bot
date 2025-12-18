using System.Diagnostics;
using OpenAI;
using OpenAI.Chat;

namespace TelegramBotService.AI;

// OpenAI API service implementation
public class OpenAiService : IAiService
{
    private readonly ChatClient _chatClient;
    private readonly OpenAIClient _client;
    private readonly AiServiceConfig _config;
    private readonly string _defaultModel;
    private bool _isAvailable;

    public string Name => "OpenAI";
    public bool IsAvailable => _isAvailable;
    public string DefaultModel => _defaultModel;

    public OpenAiService(AiServiceConfig config)
    {
        _config = config;
        _isAvailable = false;

        if (!config.Enabled)
        {
            Console.WriteLine("[OpenAI] Service disabled in configuration");
            return;
        }

        if (string.IsNullOrEmpty(config.OpenAiApiKey))
        {
            Console.WriteLine("[OpenAI] API key not configured");
            return;
        }

        try
        {
            _defaultModel = string.IsNullOrEmpty(config.OpenAiModel) ? "gpt-4o-mini" : config.OpenAiModel;
            _client = new OpenAIClient(config.OpenAiApiKey);
            _chatClient = _client.GetChatClient(_defaultModel);
            _isAvailable = true;
            Console.WriteLine($"[OpenAI] Service initialized with model: {_defaultModel}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OpenAI] Failed to initialize: {ex.Message}");
            _isAvailable = false;
        }
    }

    public async Task<List<string>> ListModelsAsync(CancellationToken ct = default)
    {
        // Return commonly used models
        // Full list requires API call which may be slow
        return await Task.FromResult(new List<string>
        {
            "gpt-4o",
            "gpt-4o-mini",
            "gpt-4-turbo",
            "gpt-4",
            "gpt-3.5-turbo",
            "o1-preview",
            "o1-mini"
        });
    }

    public async Task<AiCompletionResult> CompleteAsync(
        string prompt,
        AiCompletionOptions options = null,
        CancellationToken ct = default)
    {
        var messages = new List<AiChatMessage> { AiChatMessage.User(prompt) };

        if (options != null && !string.IsNullOrEmpty(options.SystemPrompt))
        {
            messages.Insert(0, AiChatMessage.System(options.SystemPrompt));
        }

        return await ChatAsync(messages, options, ct);
    }

    public async Task<AiCompletionResult> ChatAsync(
        List<AiChatMessage> messages,
        AiCompletionOptions options = null,
        CancellationToken ct = default)
    {
        if (!_isAvailable)
        {
            return AiCompletionResult.Unavailable("OpenAI service not available");
        }

        options = options ?? new AiCompletionOptions();
        var sw = Stopwatch.StartNew();

        try
        {
            // Use specified model or default
            var modelToUse = string.IsNullOrEmpty(options.Model) ? _defaultModel : options.Model;
            var chatClient = modelToUse == _defaultModel ? _chatClient : _client.GetChatClient(modelToUse);

            // Convert messages to OpenAI format
            var chatMessages = new List<ChatMessage>();
            foreach (var msg in messages)
            {
                switch (msg.Role.ToLower())
                {
                    case "system":
                        chatMessages.Add(ChatMessage.CreateSystemMessage(msg.Content));
                        break;
                    case "assistant":
                        chatMessages.Add(ChatMessage.CreateAssistantMessage(msg.Content));
                        break;
                    case "user":
                    default:
                        chatMessages.Add(ChatMessage.CreateUserMessage(msg.Content));
                        break;
                }
            }

            // Build options
            var chatOptions = new ChatCompletionOptions
            {
                MaxOutputTokenCount = options.MaxTokens,
                Temperature = options.Temperature
            };

            // Call API
            var response = await chatClient.CompleteChatAsync(chatMessages, chatOptions, ct);
            sw.Stop();

            var completion = response.Value;
            var text = completion.Content.Count > 0 ? completion.Content[0].Text : "";

            var result = AiCompletionResult.Ok(
                text: text,
                model: completion.Model,
                promptTokens: completion.Usage?.InputTokenCount ?? 0,
                completionTokens: completion.Usage?.OutputTokenCount ?? 0
            );
            result.DurationMs = sw.ElapsedMilliseconds;

            Console.WriteLine($"[OpenAI] Completed in {sw.ElapsedMilliseconds}ms, tokens: {result.TotalTokens}");
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            Console.WriteLine($"[OpenAI] Error: {ex.Message}");
            return AiCompletionResult.Fail(ex.Message);
        }
    }

    public async Task<AiCompletionResult> SummarizeAsync(
        string text,
        string instruction = null,
        AiCompletionOptions options = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return AiCompletionResult.Fail("Text to summarize is empty");
        }

        var defaultInstruction = "Please provide a concise summary of the following content. " +
                                 "Focus on key points, main ideas, and important details.";

        var systemPrompt = instruction ?? defaultInstruction;

        var messages = new List<AiChatMessage>
        {
            AiChatMessage.System(systemPrompt),
            AiChatMessage.User(text)
        };

        return await ChatAsync(messages, options, ct);
    }

    public async Task<AiCompletionResult> AnswerAsync(
        string question,
        string context,
        AiCompletionOptions options = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return AiCompletionResult.Fail("Question is empty");
        }

        var systemPrompt = "You are a helpful assistant. Answer the user's question based on the provided context. " +
                          "If the context doesn't contain relevant information, say so honestly. " +
                          "Be concise and accurate.";

        var userMessage = string.IsNullOrWhiteSpace(context)
            ? question
            : $"Context:\n{context}\n\nQuestion: {question}";

        var messages = new List<AiChatMessage>
        {
            AiChatMessage.System(systemPrompt),
            AiChatMessage.User(userMessage)
        };

        return await ChatAsync(messages, options, ct);
    }
}
