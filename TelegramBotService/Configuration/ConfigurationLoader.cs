using Microsoft.Extensions.Configuration;

namespace TelegramBotService.Configuration;

// Loads configuration from appsettings.json and environment variables
public static class ConfigurationLoader
{
    // Load configuration from file and environment variables
    public static BotConfiguration Load(string basePath = null, string configFileName = "appsettings.json")
    {
        basePath = basePath ?? Directory.GetCurrentDirectory();

        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile(configFileName, optional: true, reloadOnChange: true)
            .AddEnvironmentVariables(prefix: "BOT_");

        var configuration = builder.Build();
        var botConfig = new BotConfiguration();
        configuration.GetSection("BotConfiguration").Bind(botConfig);

        // Override with environment variables
        OverrideFromEnvironment(botConfig);

        return botConfig;
    }

    // Override configuration from environment variables
    private static void OverrideFromEnvironment(BotConfiguration config)
    {
        // Storage
        config.Storage.BasePath = GetEnv("STORAGE_PATH", config.Storage.BasePath);
        config.Storage.DatabasePath = GetEnv("DATABASE_PATH", config.Storage.DatabasePath);
        config.Storage.MaxFileSizeMB = GetEnvLong("MAX_FILE_SIZE_MB", config.Storage.MaxFileSizeMB);

        // Media Conversion
        config.MediaConversion.Enabled = GetEnvBool("ENABLE_CONVERSION", config.MediaConversion.Enabled);
        config.MediaConversion.WhisperPath = GetEnv("WHISPER_PATH", config.MediaConversion.WhisperPath);
        config.MediaConversion.WhisperModel = GetEnv("WHISPER_MODEL", config.MediaConversion.WhisperModel);
        config.MediaConversion.TesseractPath = GetEnv("TESSERACT_PATH", config.MediaConversion.TesseractPath);
        config.MediaConversion.TesseractLanguages = GetEnv("TESSERACT_LANGUAGES", config.MediaConversion.TesseractLanguages);
        config.MediaConversion.FfmpegPath = GetEnv("FFMPEG_PATH", config.MediaConversion.FfmpegPath);
        config.MediaConversion.TimeoutSeconds = GetEnvInt("CONVERSION_TIMEOUT", config.MediaConversion.TimeoutSeconds);
        config.MediaConversion.MaxConcurrent = GetEnvInt("MAX_CONCURRENT", config.MediaConversion.MaxConcurrent);

        // Memory
        config.Memory.Enabled = GetEnvBool("ENABLE_MEMORY", config.Memory.Enabled);
        config.Memory.Backend = GetEnv("MEMORY_BACKEND", config.Memory.Backend);
        config.Memory.EmbeddingProvider = GetEnv("EMBEDDING_PROVIDER", config.Memory.EmbeddingProvider);
        config.Memory.SqliteStorePath = GetEnv("MEMORY_SQLITE_PATH", config.Memory.SqliteStorePath);
        config.Memory.QdrantEndpoint = GetEnv("QDRANT_ENDPOINT", config.Memory.QdrantEndpoint);
        config.Memory.EmbeddingEndpoint = GetEnv("EMBEDDING_ENDPOINT", config.Memory.EmbeddingEndpoint);
        config.Memory.EmbeddingApiKey = GetEnv("EMBEDDING_API_KEY", config.Memory.EmbeddingApiKey);
        config.Memory.EmbeddingModel = GetEnv("EMBEDDING_MODEL", config.Memory.EmbeddingModel);
        config.Memory.EmbeddingMaxTokens = GetEnvInt("EMBEDDING_MAX_TOKENS", config.Memory.EmbeddingMaxTokens);

        // AI
        config.Ai.Enabled = GetEnvBool("ENABLE_AI", config.Ai.Enabled);
        config.Ai.Provider = GetEnv("AI_PROVIDER", config.Ai.Provider);
        config.Ai.Model = GetEnv("AI_MODEL", config.Ai.Model);
        config.Ai.OllamaEndpoint = GetEnv("OLLAMA_ENDPOINT", config.Ai.OllamaEndpoint);
        config.Ai.OpenAiApiKey = GetEnv("OPENAI_API_KEY", config.Ai.OpenAiApiKey);
        config.Ai.OpenAiModel = GetEnv("OPENAI_MODEL", config.Ai.OpenAiModel);
    }

    private static string GetEnv(string key, string defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrEmpty(value) ? defaultValue : value;
    }

    private static bool GetEnvBool(string key, bool defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(value))
        {
            return defaultValue;
        }
        return value.ToLower() == "true" || value == "1";
    }

    private static int GetEnvInt(string key, int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(value))
        {
            return defaultValue;
        }
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    private static long GetEnvLong(string key, long defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(value))
        {
            return defaultValue;
        }
        return long.TryParse(value, out var result) ? result : defaultValue;
    }

    // Print configuration for debugging
    public static void PrintConfiguration(BotConfiguration config)
    {
        Console.WriteLine("[Configuration] Loaded settings:");
        Console.WriteLine($"  Storage.BasePath: {config.Storage.BasePath}");
        Console.WriteLine($"  Storage.DatabasePath: {config.Storage.DatabasePath}");
        Console.WriteLine($"  Storage.MaxFileSizeMB: {config.Storage.MaxFileSizeMB}");
        Console.WriteLine($"  MediaConversion.Enabled: {config.MediaConversion.Enabled}");
        Console.WriteLine($"  MediaConversion.WhisperModel: {config.MediaConversion.WhisperModel}");
        Console.WriteLine($"  Memory.Enabled: {config.Memory.Enabled}");
        Console.WriteLine($"  Memory.Backend: {config.Memory.Backend}");
        Console.WriteLine($"  Ai.Enabled: {config.Ai.Enabled}");
        Console.WriteLine($"  Ai.Provider: {config.Ai.Provider}");
    }
}
