using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TelegramBotService.AI;

// Ollama API service implementation for local LLM
public class OllamaService : IAiService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly AiServiceConfig _config;
    private readonly string _baseUrl;
    private readonly string _defaultModel;
    private bool _isAvailable;
    private bool _disposed;

    public string Name => "Ollama";
    public bool IsAvailable => _isAvailable;
    public string DefaultModel => _defaultModel;

    public OllamaService(AiServiceConfig config)
    {
        _config = config;
        _isAvailable = false;

        if (!config.Enabled)
        {
            Console.WriteLine("[Ollama] Service disabled in configuration");
            return;
        }

        _baseUrl = string.IsNullOrEmpty(config.OllamaEndpoint)
            ? "http://localhost:11434"
            : config.OllamaEndpoint.TrimEnd('/');

        _defaultModel = string.IsNullOrEmpty(config.Model) ? "llama3" : config.Model;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromMinutes(5)  // LLMs can be slow
        };

        // Check availability
        _isAvailable = CheckAvailability().GetAwaiter().GetResult();

        if (_isAvailable)
        {
            Console.WriteLine($"[Ollama] Service initialized: {_baseUrl}, model: {_defaultModel}");
        }
        else
        {
            Console.WriteLine($"[Ollama] Service not available at {_baseUrl}");
        }
    }

    private async Task<bool> CheckAvailability()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> ListModelsAsync(CancellationToken ct = default)
    {
        if (!_isAvailable)
        {
            return new List<string>();
        }

        try
        {
            var response = await _httpClient.GetAsync("/api/tags", ct);
            if (!response.IsSuccessStatusCode)
            {
                return new List<string>();
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<OllamaTagsResponse>(json);

            return result?.Models?.Select(m => m.Name).ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Ollama] Failed to list models: {ex.Message}");
            return new List<string>();
        }
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
            return AiCompletionResult.Unavailable("Ollama service not available");
        }

        options = options ?? new AiCompletionOptions();
        var sw = Stopwatch.StartNew();

        try
        {
            var modelToUse = string.IsNullOrEmpty(options.Model) ? _defaultModel : options.Model;

            // Convert messages to Ollama format
            var ollamaMessages = messages.Select(m => new OllamaChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList();

            var request = new OllamaChatRequest
            {
                Model = modelToUse,
                Messages = ollamaMessages,
                Stream = false,
                Options = new OllamaOptions
                {
                    Temperature = options.Temperature,
                    NumPredict = options.MaxTokens
                }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/chat", content, ct);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                return AiCompletionResult.Fail($"HTTP {response.StatusCode}: {error}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var chatResponse = JsonSerializer.Deserialize<OllamaChatResponse>(responseJson);

            if (chatResponse == null)
            {
                return AiCompletionResult.Fail("Failed to parse Ollama response");
            }

            var result = AiCompletionResult.Ok(
                text: chatResponse.Message?.Content ?? "",
                model: chatResponse.Model,
                promptTokens: chatResponse.PromptEvalCount,
                completionTokens: chatResponse.EvalCount
            );
            result.DurationMs = sw.ElapsedMilliseconds;

            Console.WriteLine($"[Ollama] Completed in {sw.ElapsedMilliseconds}ms, tokens: {result.TotalTokens}");
            return result;
        }
        catch (TaskCanceledException)
        {
            return AiCompletionResult.Fail("Request timed out");
        }
        catch (Exception ex)
        {
            sw.Stop();
            Console.WriteLine($"[Ollama] Error: {ex.Message}");
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient?.Dispose();
    }
}

// Ollama API request/response models

internal class OllamaChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("messages")]
    public List<OllamaChatMessage> Messages { get; set; } = new();

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;

    [JsonPropertyName("options")]
    public OllamaOptions Options { get; set; } = new();
}

internal class OllamaChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

internal class OllamaOptions
{
    [JsonPropertyName("temperature")]
    public float Temperature { get; set; } = 0.7f;

    [JsonPropertyName("num_predict")]
    public int NumPredict { get; set; } = 2048;
}

internal class OllamaChatResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("message")]
    public OllamaChatMessage Message { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("prompt_eval_count")]
    public int PromptEvalCount { get; set; }

    [JsonPropertyName("eval_count")]
    public int EvalCount { get; set; }
}

internal class OllamaTagsResponse
{
    [JsonPropertyName("models")]
    public List<OllamaModel> Models { get; set; } = new();
}

internal class OllamaModel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
