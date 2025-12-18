using Xunit;
using TelegramBotService.AI;

namespace TelegramBotService.Tests;

public class AiServiceTests
{
    // Unit tests for AI service structure and basic behavior

    [Fact]
    public void AiCompletionResult_Ok_Should_Set_Success_True()
    {
        // Act
        var result = AiCompletionResult.Ok("test response", "gpt-4", 10, 20);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("test response", result.Text);
        Assert.Equal("gpt-4", result.Model);
        Assert.Equal(10, result.PromptTokens);
        Assert.Equal(20, result.CompletionTokens);
        Assert.Equal(30, result.TotalTokens);
        Assert.Empty(result.Error);
    }

    [Fact]
    public void AiCompletionResult_Fail_Should_Set_Success_False()
    {
        // Act
        var result = AiCompletionResult.Fail("error message");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("error message", result.Error);
        Assert.Empty(result.Text);
    }

    [Fact]
    public void AiCompletionResult_Unavailable_Should_Set_Success_False()
    {
        // Act
        var result = AiCompletionResult.Unavailable("service down");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("service down", result.Error);
    }

    [Fact]
    public void AiChatMessage_Static_Methods_Should_Create_Correct_Roles()
    {
        // Act
        var system = AiChatMessage.System("system content");
        var user = AiChatMessage.User("user content");
        var assistant = AiChatMessage.Assistant("assistant content");

        // Assert
        Assert.Equal("system", system.Role);
        Assert.Equal("system content", system.Content);

        Assert.Equal("user", user.Role);
        Assert.Equal("user content", user.Content);

        Assert.Equal("assistant", assistant.Role);
        Assert.Equal("assistant content", assistant.Content);
    }

    [Fact]
    public void AiCompletionOptions_Should_Have_Default_Values()
    {
        // Act
        var options = new AiCompletionOptions();

        // Assert
        Assert.Empty(options.Model);
        Assert.Equal(0.7f, options.Temperature);
        Assert.Equal(2048, options.MaxTokens);
        Assert.Empty(options.SystemPrompt);
        Assert.False(options.Stream);
    }

    // OpenAI service tests

    [Fact]
    public void OpenAiService_Should_Not_Be_Available_When_Disabled()
    {
        // Arrange
        var config = new AiServiceConfig { Enabled = false };

        // Act
        var service = new OpenAiService(config);

        // Assert
        Assert.False(service.IsAvailable);
        Assert.Equal("OpenAI", service.Name);
    }

    [Fact]
    public void OpenAiService_Should_Not_Be_Available_When_No_ApiKey()
    {
        // Arrange
        var config = new AiServiceConfig
        {
            Enabled = true,
            Provider = "openai",
            OpenAiApiKey = ""
        };

        // Act
        var service = new OpenAiService(config);

        // Assert
        Assert.False(service.IsAvailable);
    }

    [Fact]
    public async Task OpenAiService_Should_Return_Unavailable_When_Not_Configured()
    {
        // Arrange
        var config = new AiServiceConfig { Enabled = false };
        var service = new OpenAiService(config);

        // Act
        var result = await service.CompleteAsync("test prompt");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not available", result.Error);
    }

    [Fact]
    public async Task OpenAiService_Should_List_Models()
    {
        // Arrange
        var config = new AiServiceConfig { Enabled = false };
        var service = new OpenAiService(config);

        // Act
        var models = await service.ListModelsAsync();

        // Assert
        Assert.NotEmpty(models);
        Assert.Contains("gpt-4o", models);
        Assert.Contains("gpt-4o-mini", models);
    }

    // Ollama service tests

    [Fact]
    public void OllamaService_Should_Not_Be_Available_When_Disabled()
    {
        // Arrange
        var config = new AiServiceConfig { Enabled = false };

        // Act
        using var service = new OllamaService(config);

        // Assert
        Assert.False(service.IsAvailable);
        Assert.Equal("Ollama", service.Name);
    }

    [Fact]
    public async Task OllamaService_Should_Return_Unavailable_When_Not_Running()
    {
        // Arrange - use unlikely endpoint (valid port but no service)
        var config = new AiServiceConfig
        {
            Enabled = true,
            Provider = "ollama",
            OllamaEndpoint = "http://localhost:59999",  // Unlikely to have service
            Model = "llama3"
        };

        using var service = new OllamaService(config);

        // Act
        var result = await service.CompleteAsync("test prompt");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not available", result.Error);
    }

    // Factory tests

    [Fact]
    public void AiServiceFactory_Should_Return_Null_When_Disabled()
    {
        // Arrange
        var config = new AiServiceConfig { Enabled = false };

        // Act
        var service = AiServiceFactory.Create(config);

        // Assert
        Assert.Null(service);
    }

    [Fact]
    public void AiServiceFactory_Should_Create_OpenAiService()
    {
        // Arrange
        var config = new AiServiceConfig
        {
            Enabled = true,
            Provider = "openai",
            OpenAiApiKey = "test-key"
        };

        // Act
        var service = AiServiceFactory.Create(config);

        // Assert
        Assert.NotNull(service);
        Assert.Equal("OpenAI", service.Name);
    }

    [Fact]
    public void AiServiceFactory_Should_Create_OllamaService()
    {
        // Arrange
        var config = new AiServiceConfig
        {
            Enabled = true,
            Provider = "ollama",
            OllamaEndpoint = "http://localhost:11434",
            Model = "llama3"
        };

        // Act
        var service = AiServiceFactory.Create(config);

        // Assert
        Assert.NotNull(service);
        Assert.Equal("Ollama", service.Name);
    }

    [Fact]
    public void AiServiceFactory_CreateOpenAi_Should_Create_Service()
    {
        // Act
        var service = AiServiceFactory.CreateOpenAi("test-key", "gpt-4");

        // Assert
        Assert.NotNull(service);
        Assert.Equal("OpenAI", service.Name);
        Assert.Equal("gpt-4", service.DefaultModel);
    }

    [Fact]
    public void AiServiceFactory_CreateOllama_Should_Create_Service()
    {
        // Act
        var service = AiServiceFactory.CreateOllama("http://localhost:11434", "llama3");

        // Assert
        Assert.NotNull(service);
        Assert.Equal("Ollama", service.Name);
        Assert.Equal("llama3", service.DefaultModel);
    }

    // Integration tests (require actual API keys or running Ollama)

    [Fact(Skip = "Requires OpenAI API key")]
    [Trait("Category", "Integration")]
    public async Task OpenAiService_Should_Complete_Request()
    {
        // Arrange
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var service = AiServiceFactory.CreateOpenAi(apiKey, "gpt-4o-mini");

        // Act
        var result = await service.CompleteAsync("Say 'Hello' in one word.");

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.Text);
        Assert.True(result.TotalTokens > 0);
    }

    [Fact(Skip = "Requires running Ollama")]
    [Trait("Category", "Integration")]
    public async Task OllamaService_Should_Complete_Request()
    {
        // Arrange
        var service = AiServiceFactory.CreateOllama("http://localhost:11434", "llama3");

        // Act
        var result = await service.CompleteAsync("Say 'Hello' in one word.");

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.Text);
    }
}
