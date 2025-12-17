using TelegramBotService.Storage;
using Xunit;

namespace TelegramBotService.Tests;

public class FileStorageTests : IDisposable
{
    private readonly string _testDir;
    private readonly FileStorage _storage;

    public FileStorageTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"file_storage_test_{Guid.NewGuid():N}");
        _storage = new FileStorage(new FileStorageConfig
        {
            BasePath = _testDir,
            OrganizeByDate = true,
            OrganizeByType = true,
            MaxFileSizeBytes = 1024 * 1024  // 1MB for tests
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    // ==================== Store Tests ====================

    [Fact]
    public async Task Store_SmallFile_Success()
    {
        // Arrange
        var data = new byte[100];
        new Random().NextBytes(data);

        // Act
        var result = await _storage.StoreAsync(data, "audio", ".mp3");

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.LocalPath);
        Assert.True(File.Exists(result.LocalPath));
        Assert.Equal(100, result.FileSize);
    }

    [Fact]
    public async Task Store_MediumFile_Success()
    {
        // Arrange
        var data = new byte[100 * 1024];  // 100KB
        new Random().NextBytes(data);

        // Act
        var result = await _storage.StoreAsync(data, "video", ".mp4");

        // Assert
        Assert.True(result.Success);
        Assert.True(File.Exists(result.LocalPath));
        Assert.Equal(100 * 1024, result.FileSize);
    }

    [Fact]
    public async Task Store_ExceedsMaxSize_Fails()
    {
        // Arrange
        var data = new byte[2 * 1024 * 1024];  // 2MB > 1MB limit

        // Act
        var result = await _storage.StoreAsync(data, "video", ".mp4");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("exceeds maximum", result.Error);
    }

    [Fact]
    public async Task Store_EmptyData_Fails()
    {
        // Arrange
        var data = Array.Empty<byte>();

        // Act
        var result = await _storage.StoreAsync(data, "audio", ".mp3");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No data", result.Error);
    }

    [Fact]
    public async Task Store_NullData_Fails()
    {
        // Act
        var result = await _storage.StoreAsync((byte[])null, "audio", ".mp3");

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Store_FromStream_Success()
    {
        // Arrange
        var data = new byte[500];
        new Random().NextBytes(data);
        using var stream = new MemoryStream(data);
        stream.Position = 0;  // Ensure stream is at the beginning

        // Act
        var result = await _storage.StoreAsync(stream, "photo", ".jpg");

        // Assert
        Assert.True(result.Success);
        Assert.True(File.Exists(result.LocalPath));
        Assert.Equal(500, result.FileSize);
    }

    [Fact]
    public async Task Store_NullStream_Fails()
    {
        // Act
        var result = await _storage.StoreAsync((Stream)null, "photo", ".jpg");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid stream", result.Error);
    }

    [Fact]
    public async Task Store_WithCustomId_UsesId()
    {
        // Arrange
        var data = new byte[100];
        var customId = "my_custom_file_id";

        // Act
        var result = await _storage.StoreAsync(data, "audio", ".mp3", customId);

        // Assert
        Assert.True(result.Success);
        Assert.Contains(customId, result.LocalPath);
    }

    // ==================== Path Structure Tests ====================

    [Fact]
    public async Task Store_CreatesTypeDirectory()
    {
        // Arrange
        var data = new byte[100];

        // Act
        var result = await _storage.StoreAsync(data, "audio", ".mp3");

        // Assert
        Assert.True(result.Success);
        Assert.Contains("audio", result.RelativePath);
    }

    [Fact]
    public async Task Store_CreatesDateDirectory()
    {
        // Arrange
        var data = new byte[100];
        var year = DateTime.UtcNow.ToString("yyyy");
        var month = DateTime.UtcNow.ToString("MM");

        // Act
        var result = await _storage.StoreAsync(data, "photo", ".jpg");

        // Assert
        Assert.True(result.Success);
        Assert.Contains(year, result.RelativePath);
        Assert.Contains(month, result.RelativePath);
    }

    // ==================== Read Tests ====================

    [Fact]
    public async Task Read_ExistingFile_ReturnsBytes()
    {
        // Arrange
        var originalData = new byte[100];
        new Random().NextBytes(originalData);
        var storeResult = await _storage.StoreAsync(originalData, "audio", ".mp3");

        // Act
        var readData = await _storage.ReadAsync(storeResult.LocalPath);

        // Assert
        Assert.NotNull(readData);
        Assert.Equal(originalData, readData);
    }

    [Fact]
    public async Task Read_NonExistingFile_ReturnsNull()
    {
        // Act
        var result = await _storage.ReadAsync("/nonexistent/path/file.mp3");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ReadStream_ExistingFile_ReturnsStream()
    {
        // Arrange
        var originalData = new byte[100];
        new Random().NextBytes(originalData);
        var storeResult = await _storage.StoreAsync(originalData, "audio", ".mp3");

        // Act
        using var stream = _storage.ReadStream(storeResult.LocalPath);

        // Assert
        Assert.NotNull(stream);
        Assert.True(stream.CanRead);

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        Assert.Equal(originalData, ms.ToArray());
    }

    // ==================== Exists Tests ====================

    [Fact]
    public async Task Exists_ExistingFile_ReturnsTrue()
    {
        // Arrange
        var data = new byte[100];
        var storeResult = await _storage.StoreAsync(data, "audio", ".mp3");

        // Act
        var exists = _storage.Exists(storeResult.LocalPath);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public void Exists_NonExistingFile_ReturnsFalse()
    {
        // Act
        var exists = _storage.Exists("/nonexistent/file.mp3");

        // Assert
        Assert.False(exists);
    }

    // ==================== Delete Tests ====================

    [Fact]
    public async Task Delete_ExistingFile_ReturnsTrue()
    {
        // Arrange
        var data = new byte[100];
        var storeResult = await _storage.StoreAsync(data, "audio", ".mp3");

        // Act
        var deleted = _storage.Delete(storeResult.LocalPath);

        // Assert
        Assert.True(deleted);
        Assert.False(File.Exists(storeResult.LocalPath));
    }

    [Fact]
    public void Delete_NonExistingFile_ReturnsFalse()
    {
        // Act
        var deleted = _storage.Delete("/nonexistent/file.mp3");

        // Assert
        Assert.False(deleted);
    }

    // ==================== GetInfo Tests ====================

    [Fact]
    public async Task GetInfo_ExistingFile_ReturnsFileInfo()
    {
        // Arrange
        var data = new byte[100];
        var storeResult = await _storage.StoreAsync(data, "audio", ".mp3");

        // Act
        var info = _storage.GetInfo(storeResult.LocalPath);

        // Assert
        Assert.NotNull(info);
        Assert.Equal(100, info.Length);
        Assert.True(info.Exists);
    }

    [Fact]
    public void GetInfo_NonExistingFile_ReturnsNull()
    {
        // Act
        var info = _storage.GetInfo("/nonexistent/file.mp3");

        // Assert
        Assert.Null(info);
    }

    // ==================== Stats Tests ====================

    [Fact]
    public async Task GetStats_AfterStoringFiles_ReturnsCorrectStats()
    {
        // Arrange
        await _storage.StoreAsync(new byte[100], "audio", ".mp3");
        await _storage.StoreAsync(new byte[200], "audio", ".mp3");
        await _storage.StoreAsync(new byte[300], "video", ".mp4");

        // Act
        var stats = _storage.GetStats();

        // Assert
        Assert.Equal(3, stats.TotalFiles);
        Assert.Equal(600, stats.TotalSizeBytes);
        Assert.True(stats.FilesByType.ContainsKey("audio"));
        Assert.True(stats.FilesByType.ContainsKey("video"));
    }

    // ==================== Copy Tests ====================

    [Fact]
    public async Task Copy_ExistingFile_Success()
    {
        // Arrange
        var sourceFile = Path.GetTempFileName();
        var data = new byte[100];
        new Random().NextBytes(data);
        await File.WriteAllBytesAsync(sourceFile, data);

        try
        {
            // Act
            var result = await _storage.CopyAsync(sourceFile, "document", ".txt");

            // Assert
            Assert.True(result.Success);
            Assert.True(File.Exists(result.LocalPath));

            var copiedData = await File.ReadAllBytesAsync(result.LocalPath);
            Assert.Equal(data, copiedData);
        }
        finally
        {
            File.Delete(sourceFile);
        }
    }

    [Fact]
    public async Task Copy_NonExistingFile_Fails()
    {
        // Act
        var result = await _storage.CopyAsync("/nonexistent/source.txt", "document", ".txt");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }

    // ==================== Concurrent Access Tests ====================

    [Fact]
    public async Task Store_ConcurrentWrites_AllSucceed()
    {
        // Arrange
        var tasks = new List<Task<StorageResult>>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            var data = new byte[100];
            new Random().NextBytes(data);
            tasks.Add(_storage.StoreAsync(data, "audio", ".mp3"));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, r => Assert.True(r.Success));
        Assert.Equal(10, results.Select(r => r.LocalPath).Distinct().Count());
    }
}
