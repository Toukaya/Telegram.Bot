namespace TelegramBotService.AI;

// Configuration for AI services
public class AiServiceConfig
{
    public bool Enabled { get; set; } = false;
    public string Provider { get; set; } = "ollama";  // ollama, openai
    public string Model { get; set; } = "llama3";
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string OpenAiApiKey { get; set; } = "";
    public string OpenAiModel { get; set; } = "gpt-4o-mini";
}
