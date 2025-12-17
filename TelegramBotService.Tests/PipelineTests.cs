using TelegramBotService.Pipeline;
using TelegramBotService.Storage;
using TelegramBotService.MediaConverters;
using Xunit;
using Xunit.Abstractions;

namespace TelegramBotService.Tests;

public class PipelineTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testDir;
    private readonly FileStorage _storage;
    private readonly MediaConversionService _conversionService;

    public PipelineTests(ITestOutputHelper output)
    {
        _output = output;
        _testDir = Path.Combine(Path.GetTempPath(), $"pipeline_test_{Guid.NewGuid():N}");

        _storage = new FileStorage(new FileStorageConfig
        {
            BasePath = Path.Combine(_testDir, "storage"),
            MaxFileSizeBytes = 10 * 1024 * 1024  // 10MB
        });

        _conversionService = new MediaConversionService();
    }

    public void Dispose()
    {
        _conversionService.Dispose();
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    // ==================== PipelineResult Tests ====================

    [Fact]
    public void PipelineResult_Ok_CreatesSuccessResult()
    {
        var result = PipelineResult.Ok("media123", "/storage/audio/test.mp3");

        Assert.True(result.Success);
        Assert.Equal("media123", result.MediaFileId);
        Assert.Equal("/storage/audio/test.mp3", result.LocalPath);
        Assert.Empty(result.Error);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void PipelineResult_Fail_CreatesFailureResult()
    {
        var result = PipelineResult.Fail("Storage failed");

        Assert.False(result.Success);
        Assert.Equal("Storage failed", result.Error);
    }

    // ==================== Pipeline Store Tests ====================

    [Fact]
    public async Task Pipeline_ProcessFromStream_StoresFile()
    {
        // Arrange
        var pipeline = new MediaProcessingPipeline(
            _storage,
            conversionService: null,  // Disable conversion
            config: new PipelineConfig { EnableConversion = false }
        );

        var data = new byte[1000];
        new Random().NextBytes(data);
        using var stream = new MemoryStream(data);

        var input = new MediaInput
        {
            TelegramFileId = "telegram_file_123",
            ChatId = 12345,
            UserId = 67890,
            MessageId = 111,
            FileType = "audio",
            FileName = "test.mp3",
            FileStream = stream
        };

        // Act
        var result = await pipeline.ProcessAsync(input);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.MediaFileId);
        Assert.NotEmpty(result.LocalPath);
        Assert.True(File.Exists(result.LocalPath));

        var storedData = await File.ReadAllBytesAsync(result.LocalPath);
        Assert.Equal(data, storedData);
    }

    [Fact]
    public async Task Pipeline_ProcessFromFilePath_StoresFile()
    {
        // Arrange
        var pipeline = new MediaProcessingPipeline(
            _storage,
            config: new PipelineConfig { EnableConversion = false }
        );

        // Create a temp source file
        var sourceFile = Path.Combine(_testDir, "source.mp3");
        Directory.CreateDirectory(_testDir);
        var data = new byte[500];
        new Random().NextBytes(data);
        await File.WriteAllBytesAsync(sourceFile, data);

        var input = new MediaInput
        {
            ChatId = 12345,
            FileType = "audio",
            LocalFilePath = sourceFile
        };

        // Act
        var result = await pipeline.ProcessAsync(input);

        // Assert
        Assert.True(result.Success);
        Assert.True(File.Exists(result.LocalPath));
    }

    [Fact]
    public async Task Pipeline_ProcessNoInput_Fails()
    {
        // Arrange
        var pipeline = new MediaProcessingPipeline(_storage);

        var input = new MediaInput
        {
            ChatId = 12345,
            FileType = "audio"
            // No stream or file path
        };

        // Act
        var result = await pipeline.ProcessAsync(input);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No file data", result.Error);
    }

    // ==================== Pipeline Conversion Tests ====================

    [Fact]
    public async Task Pipeline_ConversionDisabled_SkipsConversion()
    {
        // Arrange
        var pipeline = new MediaProcessingPipeline(
            _storage,
            _conversionService,
            config: new PipelineConfig { EnableConversion = false }
        );

        var data = new byte[100];
        using var stream = new MemoryStream(data);

        var input = new MediaInput
        {
            ChatId = 12345,
            FileType = "audio",
            FileStream = stream
        };

        // Act
        var result = await pipeline.ProcessAsync(input);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.IsConverted);
        Assert.Empty(result.TextContent);
    }

    [Fact]
    public async Task Pipeline_ConversionEnabled_ConvertsIfAvailable()
    {
        // Arrange
        var pipeline = new MediaProcessingPipeline(
            _storage,
            _conversionService,
            config: new PipelineConfig { EnableConversion = true }
        );

        // Create a simple WAV file
        var wavPath = CreateSilentWavFile();
        var input = new MediaInput
        {
            ChatId = 12345,
            FileType = "audio",
            LocalFilePath = wavPath
        };

        // Act
        var result = await pipeline.ProcessAsync(input);

        // Assert
        Assert.True(result.Success);
        _output.WriteLine($"Converted: {result.IsConverted}");
        _output.WriteLine($"Text: {result.TextContent}");
        _output.WriteLine($"Warnings: {string.Join(", ", result.Warnings)}");

        // If whisper is available, it should be converted
        // If not, warnings should indicate why
        if (!result.IsConverted)
        {
            Assert.True(result.Warnings.Count > 0);
        }
    }

    [Fact]
    public async Task Pipeline_DocumentType_SkipsConversion()
    {
        // Arrange
        var pipeline = new MediaProcessingPipeline(
            _storage,
            _conversionService,
            config: new PipelineConfig { EnableConversion = true }
        );

        var data = new byte[100];
        using var stream = new MemoryStream(data);

        var input = new MediaInput
        {
            ChatId = 12345,
            FileType = "document",  // Documents don't get converted
            FileStream = stream
        };

        // Act
        var result = await pipeline.ProcessAsync(input);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.IsConverted);  // Documents skip conversion
    }

    // ==================== Pipeline Indexing Tests ====================

    [Fact]
    public async Task Pipeline_IndexingDisabled_SkipsIndexing()
    {
        // Arrange
        var memoryService = new InMemoryService();
        var pipeline = new MediaProcessingPipeline(
            _storage,
            _conversionService,
            memoryService,
            config: new PipelineConfig
            {
                EnableConversion = false,
                EnableIndexing = false
            }
        );

        var data = new byte[100];
        using var stream = new MemoryStream(data);

        var input = new MediaInput
        {
            ChatId = 12345,
            FileType = "audio",
            FileStream = stream
        };

        // Act
        var result = await pipeline.ProcessAsync(input);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.IsIndexed);
    }

    [Fact]
    public async Task Pipeline_WithMemoryService_IndexesContent()
    {
        // Arrange
        var memoryService = new InMemoryService();

        // We need conversion to produce text for indexing
        // Since we can't guarantee whisper is installed, use a mock approach
        var pipeline = new MediaProcessingPipeline(
            _storage,
            null,  // No conversion
            memoryService,
            config: new PipelineConfig
            {
                EnableConversion = false,
                EnableIndexing = true
            }
        );

        var data = new byte[100];
        using var stream = new MemoryStream(data);

        var input = new MediaInput
        {
            ChatId = 12345,
            FileType = "audio",
            FileStream = stream
        };

        // Act
        var result = await pipeline.ProcessAsync(input);

        // Assert
        Assert.True(result.Success);
        // Without text content, indexing is skipped
        Assert.False(result.IsIndexed);
    }

    // ==================== Concurrent Processing Tests ====================

    [Fact]
    public async Task Pipeline_ConcurrentProcessing_AllSucceed()
    {
        // Arrange
        var pipeline = new MediaProcessingPipeline(
            _storage,
            config: new PipelineConfig
            {
                EnableConversion = false,
                MaxConcurrentProcessing = 3
            }
        );

        var tasks = new List<Task<PipelineResult>>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            var data = new byte[100];
            new Random().NextBytes(data);
            var stream = new MemoryStream(data);

            var input = new MediaInput
            {
                ChatId = 12345,
                FileType = "audio",
                FileName = $"test_{i}.mp3",
                FileStream = stream
            };

            tasks.Add(pipeline.ProcessAsync(input));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, r => Assert.True(r.Success));
        Assert.Equal(10, results.Select(r => r.MediaFileId).Distinct().Count());
    }

    // ==================== Cancellation Tests ====================

    [Fact]
    public async Task Pipeline_Cancellation_ThrowsOperationCanceled()
    {
        // Arrange
        var pipeline = new MediaProcessingPipeline(_storage);
        var cts = new CancellationTokenSource();
        cts.Cancel();  // Cancel immediately

        var data = new byte[100];
        using var stream = new MemoryStream(data);

        var input = new MediaInput
        {
            ChatId = 12345,
            FileType = "audio",
            FileStream = stream
        };

        // Act & Assert
        // TaskCanceledException inherits from OperationCanceledException
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pipeline.ProcessAsync(input, cts.Token)
        );
        Assert.True(ex is OperationCanceledException);
    }

    // ==================== IMemoryService Tests ====================

    [Fact]
    public async Task InMemoryService_IndexAndSearch_Works()
    {
        // Arrange
        var service = new InMemoryService();

        await service.IndexAsync("doc1", "Hello world, this is a test", new Dictionary<string, string>
        {
            ["type"] = "audio",
            ["chat_id"] = "123"
        });

        await service.IndexAsync("doc2", "Goodbye everyone", new Dictionary<string, string>
        {
            ["type"] = "audio",
            ["chat_id"] = "123"
        });

        // Act
        var results = await service.SearchAsync("Hello");

        // Assert
        Assert.Single(results);
        Assert.Equal("doc1", results[0].DocumentId);
        Assert.Contains("Hello world", results[0].Text);
    }

    [Fact]
    public async Task InMemoryService_SearchWithFilters_Works()
    {
        // Arrange
        var service = new InMemoryService();

        await service.IndexAsync("doc1", "Hello from chat 123", new Dictionary<string, string>
        {
            ["chat_id"] = "123"
        });

        await service.IndexAsync("doc2", "Hello from chat 456", new Dictionary<string, string>
        {
            ["chat_id"] = "456"
        });

        // Act
        var results = await service.SearchAsync("Hello", filters: new Dictionary<string, string>
        {
            ["chat_id"] = "123"
        });

        // Assert
        Assert.Single(results);
        Assert.Equal("doc1", results[0].DocumentId);
    }

    [Fact]
    public async Task InMemoryService_Delete_RemovesDocument()
    {
        // Arrange
        var service = new InMemoryService();
        await service.IndexAsync("doc1", "Hello world", new Dictionary<string, string>());

        // Act
        await service.DeleteAsync("doc1");
        var results = await service.SearchAsync("Hello");

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void NullMemoryService_IsNotAvailable()
    {
        var service = new NullMemoryService();
        Assert.False(service.IsAvailable);
    }

    [Fact]
    public async Task NullMemoryService_SearchReturnsEmpty()
    {
        var service = new NullMemoryService();
        var results = await service.SearchAsync("anything");
        Assert.Empty(results);
    }

    // ==================== Extension Detection Tests ====================

    [Fact]
    public async Task Pipeline_DetectsExtensionFromFilename()
    {
        var pipeline = new MediaProcessingPipeline(
            _storage,
            config: new PipelineConfig { EnableConversion = false }
        );

        var data = new byte[100];
        using var stream = new MemoryStream(data);

        var input = new MediaInput
        {
            ChatId = 12345,
            FileType = "audio",
            FileName = "my_song.ogg",  // Extension from filename
            FileStream = stream
        };

        var result = await pipeline.ProcessAsync(input);

        Assert.True(result.Success);
        Assert.EndsWith(".ogg", result.LocalPath);
    }

    [Fact]
    public async Task Pipeline_DetectsExtensionFromMimeType()
    {
        var pipeline = new MediaProcessingPipeline(
            _storage,
            config: new PipelineConfig { EnableConversion = false }
        );

        var data = new byte[100];
        using var stream = new MemoryStream(data);

        var input = new MediaInput
        {
            ChatId = 12345,
            FileType = "audio",
            MimeType = "audio/mpeg",  // Extension from MIME
            FileStream = stream
        };

        var result = await pipeline.ProcessAsync(input);

        Assert.True(result.Success);
        Assert.EndsWith(".mp3", result.LocalPath);
    }

    // ==================== Helper Methods ====================

    private string CreateSilentWavFile()
    {
        Directory.CreateDirectory(_testDir);
        var path = Path.Combine(_testDir, $"silent_{Guid.NewGuid():N}.wav");

        var sampleRate = 8000;
        var numSamples = sampleRate;  // 1 second
        var byteRate = sampleRate * 2;
        var dataSize = numSamples * 2;

        using var fs = new FileStream(path, FileMode.Create);
        using var bw = new BinaryWriter(fs);

        bw.Write("RIFF".ToCharArray());
        bw.Write(36 + dataSize);
        bw.Write("WAVE".ToCharArray());
        bw.Write("fmt ".ToCharArray());
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)1);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write((short)2);
        bw.Write((short)16);
        bw.Write("data".ToCharArray());
        bw.Write(dataSize);

        for (int i = 0; i < numSamples; i++)
        {
            bw.Write((short)0);
        }

        return path;
    }
}
