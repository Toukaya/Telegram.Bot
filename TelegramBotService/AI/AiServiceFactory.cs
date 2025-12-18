using TelegramBotService.Configuration;

namespace TelegramBotService.AI;

// Factory for creating AI services based on configuration
public static class AiServiceFactory
{
    // Create AI service based on configuration
    public static IAiService Create(AiConfig config)
    {
        if (config == null || !config.Enabled)
        {
            Console.WriteLine("[AiService] AI service disabled");
            return null;
        }

        var provider = config.Provider?.ToLower() ?? "openai";

        switch (provider)
        {
            case "openai":
                Console.WriteLine("[AiService] Creating OpenAI service");
                return new OpenAiService(config);

            case "ollama":
                Console.WriteLine("[AiService] Creating Ollama service");
                return new OllamaService(config);

            default:
                Console.WriteLine($"[AiService] Unknown provider: {provider}, defaulting to OpenAI");
                return new OpenAiService(config);
        }
    }

    // Create service with explicit provider
    public static IAiService CreateOpenAi(string apiKey, string model = "gpt-4o-mini")
    {
        var config = new AiConfig
        {
            Enabled = true,
            Provider = "openai",
            OpenAiApiKey = apiKey,
            OpenAiModel = model
        };
        return new OpenAiService(config);
    }

    // Create Ollama service with explicit endpoint
    public static IAiService CreateOllama(string endpoint = "http://localhost:11434", string model = "llama3")
    {
        var config = new AiConfig
        {
            Enabled = true,
            Provider = "ollama",
            OllamaEndpoint = endpoint,
            Model = model
        };
        return new OllamaService(config);
    }
}
