using Xunit;
using TelegramBotService.Memory;
using TelegramBotService.Configuration;

namespace TelegramBotService.Tests;

// Note: Full KernelMemory tests require embedding infrastructure (OpenAI API key or local model)
// Tests marked with [Trait("Category", "Integration")] require proper configuration
public class KernelMemoryServiceTests : IDisposable
{
    private readonly string _testStoragePath;

    public KernelMemoryServiceTests()
    {
        _testStoragePath = Path.Combine(Path.GetTempPath(), $"km_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testStoragePath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testStoragePath))
            {
                Directory.Delete(_testStoragePath, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public void Should_Not_Be_Available_When_Disabled()
    {
        // Arrange
        var config = new MemoryConfig
        {
            Enabled = false
        };

        // Act
        using var service = new KernelMemoryService(config);

        // Assert
        Assert.False(service.IsAvailable);
    }

    [Fact]
    public void Should_Return_Status_Info_When_Disabled()
    {
        // Arrange
        var config = new MemoryConfig
        {
            Enabled = false,
            Backend = "sqlite",
            EmbeddingProvider = "local",
            SqliteStorePath = Path.Combine(_testStoragePath, "memory.db")
        };

        // Act
        using var service = new KernelMemoryService(config);
        var status = service.GetStatus();

        // Assert
        Assert.Contains("available", status.Keys);
        Assert.Contains("backend", status.Keys);
        Assert.Equal("False", status["available"]);
    }

    [Fact]
    public async Task Should_Throw_When_Not_Available()
    {
        // Arrange
        var config = new MemoryConfig
        {
            Enabled = false
        };

        using var service = new KernelMemoryService(config);
        Assert.False(service.IsAvailable);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.IndexAsync("doc", "text", new Dictionary<string, string>()));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SearchAsync("query"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync("doc"));
    }

    // Integration tests - require embedding infrastructure
    // To run these tests, set OPENAI_API_KEY environment variable or configure local embedding model

    [Fact(Skip = "Requires embedding infrastructure (OpenAI API key or local model)")]
    [Trait("Category", "Integration")]
    public void Should_Be_Available_When_Enabled_With_Valid_Config()
    {
        // Arrange
        var config = new MemoryConfig
        {
            Enabled = true,
            Backend = "sqlite",
            EmbeddingProvider = "local",
            SqliteStorePath = Path.Combine(_testStoragePath, "memory.db")
        };

        // Act
        using var service = new KernelMemoryService(config);

        // Assert
        Assert.True(service.IsAvailable);
    }

    [Fact(Skip = "Requires embedding infrastructure (OpenAI API key or local model)")]
    [Trait("Category", "Integration")]
    public async Task Should_Index_And_Search_Text()
    {
        // Arrange
        var config = new MemoryConfig
        {
            Enabled = true,
            Backend = "sqlite",
            EmbeddingProvider = "local",
            SqliteStorePath = Path.Combine(_testStoragePath, "memory.db")
        };

        using var service = new KernelMemoryService(config);
        Assert.True(service.IsAvailable);

        var documentId = "test_doc_1";
        var text = "This is a test document about machine learning and artificial intelligence.";
        var metadata = new Dictionary<string, string>
        {
            ["file_type"] = "audio",
            ["chat_id"] = "12345"
        };

        // Act - Index
        await service.IndexAsync(documentId, text, metadata);
        await Task.Delay(500);

        // Act - Search
        var results = await service.SearchAsync("machine learning", limit: 5);

        // Assert
        Assert.NotNull(results);
    }

    [Fact(Skip = "Requires embedding infrastructure (OpenAI API key or local model)")]
    [Trait("Category", "Integration")]
    public async Task Should_Delete_Document()
    {
        // Arrange
        var config = new MemoryConfig
        {
            Enabled = true,
            Backend = "sqlite",
            EmbeddingProvider = "local",
            SqliteStorePath = Path.Combine(_testStoragePath, "memory.db")
        };

        using var service = new KernelMemoryService(config);
        Assert.True(service.IsAvailable);

        var documentId = "test_doc_delete";
        await service.IndexAsync(documentId, "Document to delete", new Dictionary<string, string>());
        await Task.Delay(500);

        // Act & Assert - no exception means success
        await service.DeleteAsync(documentId);
    }
}
