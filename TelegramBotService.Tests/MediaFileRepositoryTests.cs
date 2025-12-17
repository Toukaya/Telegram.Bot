using BotDatabase;
using BotDatabase.Entities;
using BotDatabase.Services;
using Xunit;

namespace TelegramBotService.Tests;

public class MediaFileRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly BotDb _db;

    public MediaFileRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_db_{Guid.NewGuid():N}.db");
        _db = new BotDb(_dbPath);
        _db.InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    // ==================== Create Tests ====================

    [Fact]
    public async Task Create_WithAutoId_GeneratesId()
    {
        // Arrange
        var file = new MediaFile
        {
            ChatId = 12345,
            UserId = 67890,
            MessageId = 111,
            FileType = MediaFileType.Audio,
            LocalPath = "/storage/audio/test.mp3"
        };

        // Act
        var created = await _db.MediaFiles.Create(file);

        // Assert
        Assert.NotNull(created);
        Assert.NotEmpty(created.Id);
        Assert.Equal(32, created.Id.Length);  // GUID without hyphens
    }

    [Fact]
    public async Task Create_WithCustomId_PreservesId()
    {
        // Arrange
        var customId = "my_custom_id_123";
        var file = new MediaFile
        {
            Id = customId,
            ChatId = 12345,
            UserId = 67890,
            MessageId = 111,
            FileType = MediaFileType.Audio
        };

        // Act
        var created = await _db.MediaFiles.Create(file);

        // Assert
        Assert.Equal(customId, created.Id);
    }

    [Fact]
    public async Task Create_SetsTimestamps()
    {
        // Arrange
        var file = new MediaFile
        {
            ChatId = 12345,
            FileType = MediaFileType.Photo
        };

        // Act
        var before = DateTime.UtcNow;
        var created = await _db.MediaFiles.Create(file);
        var after = DateTime.UtcNow;

        // Assert
        Assert.True(created.CreatedAt >= before && created.CreatedAt <= after);
        Assert.True(created.UpdatedAt >= before && created.UpdatedAt <= after);
    }

    // ==================== Find Tests ====================

    [Fact]
    public async Task Find_ExistingId_ReturnsEntity()
    {
        // Arrange
        var file = await _db.MediaFiles.Create(new MediaFile
        {
            ChatId = 12345,
            FileType = MediaFileType.Audio
        });

        // Act
        var found = await _db.MediaFiles.Find(file.Id);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(file.Id, found.Id);
        Assert.Equal(12345, found.ChatId);
    }

    [Fact]
    public async Task Find_NonExistingId_ReturnsNull()
    {
        // Act
        var found = await _db.MediaFiles.Find("nonexistent_id");

        // Assert
        Assert.Null(found);
    }

    [Fact]
    public async Task FindByTelegramFileId_ExistingFile_ReturnsEntity()
    {
        // Arrange
        var telegramId = "AgACAgIAAxkBAAI";
        await _db.MediaFiles.Create(new MediaFile
        {
            TelegramFileUniqueId = telegramId,
            ChatId = 12345,
            FileType = MediaFileType.Photo
        });

        // Act
        var found = await _db.MediaFiles.FindByTelegramFileId(telegramId);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(telegramId, found.TelegramFileUniqueId);
    }

    [Fact]
    public async Task Exists_ExistingTelegramId_ReturnsTrue()
    {
        // Arrange
        var telegramId = "unique_file_id_123";
        await _db.MediaFiles.Create(new MediaFile
        {
            TelegramFileUniqueId = telegramId,
            ChatId = 12345,
            FileType = MediaFileType.Audio
        });

        // Act
        var exists = await _db.MediaFiles.Exists(telegramId);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task Exists_NonExistingTelegramId_ReturnsFalse()
    {
        // Act
        var exists = await _db.MediaFiles.Exists("nonexistent_telegram_id");

        // Assert
        Assert.False(exists);
    }

    // ==================== Update Tests ====================

    [Fact]
    public async Task UpdateConvertStatus_UpdatesFields()
    {
        // Arrange
        var file = await _db.MediaFiles.Create(new MediaFile
        {
            ChatId = 12345,
            FileType = MediaFileType.Audio,
            ConvertStatus = MediaConvertStatus.Pending
        });

        // Act
        await _db.MediaFiles.UpdateConvertStatus(file.Id, MediaConvertStatus.Completed, "Hello world", null);

        // Assert
        var updated = await _db.MediaFiles.Find(file.Id);
        Assert.Equal(MediaConvertStatus.Completed, updated.ConvertStatus);
        Assert.Equal("Hello world", updated.TextContent);
        Assert.True(updated.ConvertedAt > DateTime.MinValue);
    }

    [Fact]
    public async Task MarkConverted_SetsStatusAndText()
    {
        // Arrange
        var file = await _db.MediaFiles.Create(new MediaFile
        {
            ChatId = 12345,
            FileType = MediaFileType.Voice
        });

        // Act
        await _db.MediaFiles.MarkConverted(file.Id, "This is the transcription");

        // Assert
        var updated = await _db.MediaFiles.Find(file.Id);
        Assert.Equal(MediaConvertStatus.Completed, updated.ConvertStatus);
        Assert.Equal("This is the transcription", updated.TextContent);
    }

    [Fact]
    public async Task MarkConvertFailed_SetsStatusAndError()
    {
        // Arrange
        var file = await _db.MediaFiles.Create(new MediaFile
        {
            ChatId = 12345,
            FileType = MediaFileType.Audio
        });

        // Act
        await _db.MediaFiles.MarkConvertFailed(file.Id, "Whisper not installed");

        // Assert
        var updated = await _db.MediaFiles.Find(file.Id);
        Assert.Equal(MediaConvertStatus.Failed, updated.ConvertStatus);
        Assert.Equal("Whisper not installed", updated.ConvertError);
    }

    [Fact]
    public async Task MarkIndexed_SetsFlag()
    {
        // Arrange
        var file = await _db.MediaFiles.Create(new MediaFile
        {
            ChatId = 12345,
            FileType = MediaFileType.Audio,
            IsIndexed = false
        });

        // Act
        await _db.MediaFiles.MarkIndexed(file.Id);

        // Assert
        var updated = await _db.MediaFiles.Find(file.Id);
        Assert.True(updated.IsIndexed);
        Assert.True(updated.IndexedAt > DateTime.MinValue);
    }

    // ==================== Delete Tests ====================

    [Fact]
    public async Task Delete_ExistingFile_ReturnsTrue()
    {
        // Arrange
        var file = await _db.MediaFiles.Create(new MediaFile
        {
            ChatId = 12345,
            FileType = MediaFileType.Photo
        });

        // Act
        var deleted = await _db.MediaFiles.Delete(file.Id);

        // Assert
        Assert.True(deleted);
        Assert.Null(await _db.MediaFiles.Find(file.Id));
    }

    [Fact]
    public async Task Delete_NonExistingFile_ReturnsFalse()
    {
        // Act
        var deleted = await _db.MediaFiles.Delete("nonexistent_id");

        // Assert
        Assert.False(deleted);
    }

    // ==================== Query Tests ====================

    [Fact]
    public async Task FromChat_ReturnsOnlyMatchingChat()
    {
        // Arrange
        await _db.MediaFiles.Create(new MediaFile { ChatId = 111, FileType = MediaFileType.Audio });
        await _db.MediaFiles.Create(new MediaFile { ChatId = 111, FileType = MediaFileType.Photo });
        await _db.MediaFiles.Create(new MediaFile { ChatId = 222, FileType = MediaFileType.Audio });

        // Act
        var results = await _db.MediaFiles.FromChat(111).ToListAsync();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(111, r.ChatId));
    }

    [Fact]
    public async Task OfType_ReturnsOnlyMatchingType()
    {
        // Arrange
        await _db.MediaFiles.Create(new MediaFile { ChatId = 111, FileType = MediaFileType.Audio });
        await _db.MediaFiles.Create(new MediaFile { ChatId = 111, FileType = MediaFileType.Photo });
        await _db.MediaFiles.Create(new MediaFile { ChatId = 111, FileType = MediaFileType.Audio });

        // Act
        var results = await _db.MediaFiles.OfType(MediaFileType.Audio).ToListAsync();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(MediaFileType.Audio, r.FileType));
    }

    [Fact]
    public async Task PendingConversion_ReturnsOnlyPending()
    {
        // Arrange
        await _db.MediaFiles.Create(new MediaFile
        {
            ChatId = 111,
            FileType = MediaFileType.Audio,
            ConvertStatus = MediaConvertStatus.Pending
        });
        await _db.MediaFiles.Create(new MediaFile
        {
            ChatId = 111,
            FileType = MediaFileType.Audio,
            ConvertStatus = MediaConvertStatus.Completed
        });

        // Act
        var results = await _db.MediaFiles.PendingConversion().ToListAsync();

        // Assert
        Assert.Single(results);
        Assert.Equal(MediaConvertStatus.Pending, results[0].ConvertStatus);
    }

    [Fact]
    public async Task NotIndexed_ReturnsOnlyNotIndexed()
    {
        // Arrange
        var file1 = await _db.MediaFiles.Create(new MediaFile
        {
            ChatId = 111,
            FileType = MediaFileType.Audio,
            IsIndexed = false
        });
        var file2 = await _db.MediaFiles.Create(new MediaFile
        {
            ChatId = 111,
            FileType = MediaFileType.Audio,
            IsIndexed = false
        });
        await _db.MediaFiles.MarkIndexed(file2.Id);

        // Act
        var results = await _db.MediaFiles.NotIndexed().ToListAsync();

        // Assert
        Assert.Single(results);
        Assert.Equal(file1.Id, results[0].Id);
    }

    [Fact]
    public async Task SearchText_FindsMatchingContent()
    {
        // Arrange
        var file1 = await _db.MediaFiles.Create(new MediaFile
        {
            ChatId = 111,
            FileType = MediaFileType.Audio,
            TextContent = "Hello world, this is a test"
        });
        await _db.MediaFiles.Create(new MediaFile
        {
            ChatId = 111,
            FileType = MediaFileType.Audio,
            TextContent = "Goodbye everyone"
        });

        // Act
        var results = await _db.MediaFiles.FromChat(111).SearchText("Hello").ToListAsync();

        // Assert
        Assert.Single(results);
        Assert.Equal(file1.Id, results[0].Id);
    }

    [Fact]
    public async Task Recent_ReturnsOrderedByDate()
    {
        // Arrange
        await _db.MediaFiles.Create(new MediaFile { ChatId = 111, FileType = MediaFileType.Audio });
        await Task.Delay(10);
        await _db.MediaFiles.Create(new MediaFile { ChatId = 111, FileType = MediaFileType.Photo });
        await Task.Delay(10);
        await _db.MediaFiles.Create(new MediaFile { ChatId = 111, FileType = MediaFileType.Video });

        // Act
        var results = await _db.MediaFiles.Recent(3).ToListAsync();

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Equal(MediaFileType.Video, results[0].FileType);  // Most recent first
        Assert.Equal(MediaFileType.Photo, results[1].FileType);
        Assert.Equal(MediaFileType.Audio, results[2].FileType);
    }

    [Fact]
    public async Task Limit_RespectsMaxCount()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            await _db.MediaFiles.Create(new MediaFile { ChatId = 111, FileType = MediaFileType.Audio });
        }

        // Act
        var results = await _db.MediaFiles.FromChat(111).Limit(5).ToListAsync();

        // Assert
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public async Task GetAllTextAsync_ReturnsOnlyNonEmptyText()
    {
        // Arrange
        await _db.MediaFiles.Create(new MediaFile
        {
            ChatId = 111,
            FileType = MediaFileType.Audio,
            TextContent = "First transcription"
        });
        await _db.MediaFiles.Create(new MediaFile
        {
            ChatId = 111,
            FileType = MediaFileType.Photo,
            TextContent = ""  // Empty
        });
        await _db.MediaFiles.Create(new MediaFile
        {
            ChatId = 111,
            FileType = MediaFileType.Voice,
            TextContent = "Second transcription"
        });

        // Act
        var texts = await _db.MediaFiles.FromChat(111).Limit(100).GetAllTextAsync();

        // Assert
        Assert.Equal(2, texts.Count);
        Assert.Contains("First transcription", texts);
        Assert.Contains("Second transcription", texts);
    }

    // ==================== Edge Cases ====================

    [Fact]
    public async Task Create_LongTextContent_Success()
    {
        // Arrange
        var longText = new string('x', 100000);  // 100K characters
        var file = new MediaFile
        {
            ChatId = 12345,
            FileType = MediaFileType.Audio,
            TextContent = longText
        };

        // Act
        var created = await _db.MediaFiles.Create(file);

        // Assert
        var found = await _db.MediaFiles.Find(created.Id);
        Assert.Equal(100000, found.TextContent.Length);
    }

    [Fact]
    public async Task Query_EmptyResult_ReturnsEmptyList()
    {
        // Act
        var results = await _db.MediaFiles.FromChat(99999).ToListAsync();

        // Assert
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task ChainedQueries_Work()
    {
        // Arrange
        await _db.MediaFiles.Create(new MediaFile
        {
            ChatId = 111,
            UserId = 1,
            FileType = MediaFileType.Audio,
            ConvertStatus = MediaConvertStatus.Completed,
            TextContent = "Hello world"
        });
        await _db.MediaFiles.Create(new MediaFile
        {
            ChatId = 111,
            UserId = 2,
            FileType = MediaFileType.Audio,
            ConvertStatus = MediaConvertStatus.Pending,
            TextContent = "Hello again"
        });

        // Act
        var results = await _db.MediaFiles
            .FromChat(111)
            .OfType(MediaFileType.Audio)
            .Converted()
            .SearchText("Hello")
            .ToListAsync();

        // Assert
        Assert.Single(results);
        Assert.Equal(1, results[0].UserId);
    }
}
