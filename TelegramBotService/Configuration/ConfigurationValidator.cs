namespace TelegramBotService.Configuration;

// Validates configuration and reports errors
public static class ConfigurationValidator
{
    // Validate configuration and return list of errors
    public static List<string> Validate(BotConfiguration config)
    {
        var errors = new List<string>();

        // Validate Storage configuration
        ValidateStorage(config.Storage, errors);

        // Validate MediaConversion configuration
        ValidateMediaConversion(config.MediaConversion, errors);

        // Validate Memory configuration
        ValidateMemory(config.Memory, errors);

        // Validate AI configuration
        ValidateAi(config.Ai, errors);

        return errors;
    }

    private static void ValidateStorage(StorageConfig config, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(config.BasePath))
        {
            errors.Add("Storage.BasePath cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(config.DatabasePath))
        {
            errors.Add("Storage.DatabasePath cannot be empty");
        }

        if (config.MaxFileSizeMB <= 0)
        {
            errors.Add("Storage.MaxFileSizeMB must be greater than 0");
        }

        if (config.MaxFileSizeMB > 2048)
        {
            errors.Add("Storage.MaxFileSizeMB cannot exceed 2048 MB (2GB)");
        }
    }

    private static void ValidateMediaConversion(MediaConversionConfig config, List<string> errors)
    {
        if (!config.Enabled)
        {
            return;  // Skip validation if disabled
        }

        if (config.TimeoutSeconds <= 0)
        {
            errors.Add("MediaConversion.TimeoutSeconds must be greater than 0");
        }

        if (config.TimeoutSeconds > 3600)
        {
            errors.Add("MediaConversion.TimeoutSeconds cannot exceed 3600 (1 hour)");
        }

        if (config.MaxConcurrent <= 0)
        {
            errors.Add("MediaConversion.MaxConcurrent must be greater than 0");
        }

        if (config.MaxConcurrent > 10)
        {
            errors.Add("MediaConversion.MaxConcurrent should not exceed 10");
        }

        if (config.RetryCount < 0)
        {
            errors.Add("MediaConversion.RetryCount cannot be negative");
        }

        if (config.RetryDelayMs < 0)
        {
            errors.Add("MediaConversion.RetryDelayMs cannot be negative");
        }

        var validModels = new[] { "tiny", "base", "small", "medium", "large", "large-v2", "large-v3" };
        if (!validModels.Contains(config.WhisperModel.ToLower()))
        {
            errors.Add($"MediaConversion.WhisperModel must be one of: {string.Join(", ", validModels)}");
        }
    }

    private static void ValidateMemory(MemoryConfig config, List<string> errors)
    {
        if (!config.Enabled)
        {
            return;  // Skip validation if disabled
        }

        var validBackends = new[] { "sqlite", "qdrant", "postgres" };
        if (!validBackends.Contains(config.Backend.ToLower()))
        {
            errors.Add($"Memory.Backend must be one of: {string.Join(", ", validBackends)}");
        }

        var validProviders = new[] { "siliconflow", "openai" };
        if (!validProviders.Contains(config.EmbeddingProvider.ToLower()))
        {
            errors.Add($"Memory.EmbeddingProvider must be one of: {string.Join(", ", validProviders)}");
        }

        // Validate embedding settings for siliconflow and openai
        if (validProviders.Contains(config.EmbeddingProvider.ToLower()))
        {
            if (string.IsNullOrWhiteSpace(config.EmbeddingApiKey))
            {
                errors.Add("Memory.EmbeddingApiKey is required when using embedding provider");
            }

            if (string.IsNullOrWhiteSpace(config.EmbeddingModel))
            {
                errors.Add("Memory.EmbeddingModel is required when using embedding provider");
            }

            if (!string.IsNullOrWhiteSpace(config.EmbeddingEndpoint) &&
                !Uri.TryCreate(config.EmbeddingEndpoint, UriKind.Absolute, out _))
            {
                errors.Add("Memory.EmbeddingEndpoint must be a valid URL");
            }

            if (config.EmbeddingMaxTokens <= 0)
            {
                errors.Add("Memory.EmbeddingMaxTokens must be greater than 0");
            }
        }

        if (config.Backend.ToLower() == "sqlite")
        {
            if (string.IsNullOrWhiteSpace(config.SqliteStorePath))
            {
                errors.Add("Memory.SqliteStorePath is required when using SQLite backend");
            }
        }

        if (config.Backend.ToLower() == "qdrant")
        {
            if (string.IsNullOrWhiteSpace(config.QdrantEndpoint))
            {
                errors.Add("Memory.QdrantEndpoint is required when using Qdrant backend");
            }

            if (!Uri.TryCreate(config.QdrantEndpoint, UriKind.Absolute, out _))
            {
                errors.Add("Memory.QdrantEndpoint must be a valid URL");
            }
        }
    }

    private static void ValidateAi(AiConfig config, List<string> errors)
    {
        if (!config.Enabled)
        {
            return;  // Skip validation if disabled
        }

        var validProviders = new[] { "ollama", "openai" };
        if (!validProviders.Contains(config.Provider.ToLower()))
        {
            errors.Add($"Ai.Provider must be one of: {string.Join(", ", validProviders)}");
        }

        if (string.IsNullOrWhiteSpace(config.Model))
        {
            errors.Add("Ai.Model cannot be empty when AI is enabled");
        }

        if (config.Provider.ToLower() == "openai")
        {
            if (string.IsNullOrWhiteSpace(config.OpenAiApiKey))
            {
                errors.Add("Ai.OpenAiApiKey is required when using OpenAI");
            }
        }

        if (config.Provider.ToLower() == "ollama")
        {
            if (string.IsNullOrWhiteSpace(config.OllamaEndpoint))
            {
                errors.Add("Ai.OllamaEndpoint is required when using Ollama");
            }

            if (!Uri.TryCreate(config.OllamaEndpoint, UriKind.Absolute, out _))
            {
                errors.Add("Ai.OllamaEndpoint must be a valid URL");
            }
        }
    }

    // Validate and throw exception if errors found
    public static void ValidateAndThrow(BotConfiguration config)
    {
        var errors = Validate(config);
        if (errors.Count > 0)
        {
            var errorMessage = "Configuration validation failed:\n" + string.Join("\n", errors.Select(e => $"  - {e}"));
            throw new InvalidOperationException(errorMessage);
        }
    }

    // Print validation result
    public static bool ValidateAndPrint(BotConfiguration config)
    {
        var errors = Validate(config);
        if (errors.Count == 0)
        {
            Console.WriteLine("[Configuration] Validation passed");
            return true;
        }

        Console.WriteLine("[Configuration] Validation failed:");
        foreach (var error in errors)
        {
            Console.WriteLine($"  - {error}");
        }
        return false;
    }
}
