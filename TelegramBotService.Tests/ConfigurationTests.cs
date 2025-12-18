using Xunit;
using TelegramBotService.Configuration;

namespace TelegramBotService.Tests;

public class ConfigurationTests
{
    [Fact]
    public void Should_Create_Default_Configuration()
    {
        // Act
        var config = new BotConfiguration();

        // Assert
        Assert.NotNull(config.Storage);
        Assert.NotNull(config.MediaConversion);
        Assert.NotNull(config.Memory);
        Assert.NotNull(config.Ai);

        Assert.Equal("./storage", config.Storage.BasePath);
        Assert.Equal("./bot.db", config.Storage.DatabasePath);
        Assert.Equal(50, config.Storage.MaxFileSizeMB);

        Assert.True(config.MediaConversion.Enabled);
        Assert.Equal("whisper", config.MediaConversion.WhisperPath);
        Assert.Equal("base", config.MediaConversion.WhisperModel);

        Assert.False(config.Memory.Enabled);
        Assert.Equal("sqlite", config.Memory.Backend);

        Assert.False(config.Ai.Enabled);
        Assert.Equal("ollama", config.Ai.Provider);
    }

    [Fact]
    public void Should_Validate_Valid_Configuration()
    {
        // Arrange
        var config = new BotConfiguration();

        // Act
        var errors = ConfigurationValidator.Validate(config);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void Should_Report_Error_For_Empty_Storage_Path()
    {
        // Arrange
        var config = new BotConfiguration();
        config.Storage.BasePath = "";

        // Act
        var errors = ConfigurationValidator.Validate(config);

        // Assert
        Assert.Contains(errors, e => e.Contains("Storage.BasePath"));
    }

    [Fact]
    public void Should_Report_Error_For_Invalid_Max_File_Size()
    {
        // Arrange
        var config = new BotConfiguration();
        config.Storage.MaxFileSizeMB = 0;

        // Act
        var errors = ConfigurationValidator.Validate(config);

        // Assert
        Assert.Contains(errors, e => e.Contains("MaxFileSizeMB"));
    }

    [Fact]
    public void Should_Report_Error_For_Excessive_Max_File_Size()
    {
        // Arrange
        var config = new BotConfiguration();
        config.Storage.MaxFileSizeMB = 5000;  // 5GB, too large

        // Act
        var errors = ConfigurationValidator.Validate(config);

        // Assert
        Assert.Contains(errors, e => e.Contains("MaxFileSizeMB") && e.Contains("exceed"));
    }

    [Fact]
    public void Should_Report_Error_For_Invalid_Whisper_Model()
    {
        // Arrange
        var config = new BotConfiguration();
        config.MediaConversion.Enabled = true;
        config.MediaConversion.WhisperModel = "invalid_model";

        // Act
        var errors = ConfigurationValidator.Validate(config);

        // Assert
        Assert.Contains(errors, e => e.Contains("WhisperModel"));
    }

    [Fact]
    public void Should_Accept_Valid_Whisper_Models()
    {
        var validModels = new[] { "tiny", "base", "small", "medium", "large", "large-v2", "large-v3" };

        foreach (var model in validModels)
        {
            // Arrange
            var config = new BotConfiguration();
            config.MediaConversion.Enabled = true;
            config.MediaConversion.WhisperModel = model;

            // Act
            var errors = ConfigurationValidator.Validate(config);

            // Assert
            Assert.DoesNotContain(errors, e => e.Contains("WhisperModel"));
        }
    }

    [Fact]
    public void Should_Skip_Validation_When_Conversion_Disabled()
    {
        // Arrange
        var config = new BotConfiguration();
        config.MediaConversion.Enabled = false;
        config.MediaConversion.WhisperModel = "invalid";  // Should be ignored

        // Act
        var errors = ConfigurationValidator.Validate(config);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void Should_Report_Error_For_Invalid_Memory_Backend()
    {
        // Arrange
        var config = new BotConfiguration();
        config.Memory.Enabled = true;
        config.Memory.Backend = "invalid_backend";

        // Act
        var errors = ConfigurationValidator.Validate(config);

        // Assert
        Assert.Contains(errors, e => e.Contains("Memory.Backend"));
    }

    [Fact]
    public void Should_Report_Error_For_Missing_OpenAI_Key()
    {
        // Arrange
        var config = new BotConfiguration();
        config.Memory.Enabled = true;
        config.Memory.EmbeddingProvider = "openai";
        config.Memory.OpenAiApiKey = "";

        // Act
        var errors = ConfigurationValidator.Validate(config);

        // Assert
        Assert.Contains(errors, e => e.Contains("OpenAiApiKey"));
    }

    [Fact]
    public void Should_Report_Error_For_Invalid_Qdrant_URL()
    {
        // Arrange
        var config = new BotConfiguration();
        config.Memory.Enabled = true;
        config.Memory.Backend = "qdrant";
        config.Memory.QdrantEndpoint = "not_a_url";

        // Act
        var errors = ConfigurationValidator.Validate(config);

        // Assert
        Assert.Contains(errors, e => e.Contains("QdrantEndpoint") && e.Contains("valid URL"));
    }

    [Fact]
    public void Should_Report_Error_For_Invalid_AI_Provider()
    {
        // Arrange
        var config = new BotConfiguration();
        config.Ai.Enabled = true;
        config.Ai.Provider = "invalid_provider";

        // Act
        var errors = ConfigurationValidator.Validate(config);

        // Assert
        Assert.Contains(errors, e => e.Contains("Ai.Provider"));
    }
}
