using TelegramBotService.Pipeline;

namespace TelegramBotService.Services;

// Metadata for indexed content
public class ContentMetadata
{
    public string Type { get; set; } = "";           // text, audio, image, video
    public long ChatId { get; set; }
    public long UserId { get; set; }
    public long MessageId { get; set; }
    public string FilePath { get; set; } = "";       // Media file path (optional)
    public string MediaFileId { get; set; } = "";    // MediaFile.Id in database
    public DateTime CreatedAt { get; set; }
}

// Service for indexing content to vector database
public class ContentIndexer
{
    private readonly IMemoryService _memory;

    public ContentIndexer(IMemoryService memory)
    {
        _memory = memory;
    }

    // Check if indexing is available
    public bool IsAvailable => _memory != null && _memory.IsAvailable;

    // Index text content to vector database
    public async Task<bool> IndexAsync(
        string documentId,
        string content,
        ContentMetadata metadata,
        CancellationToken ct = default)
    {
        if (_memory == null || !_memory.IsAvailable)
        {
            Console.WriteLine("[ContentIndexer] Memory service not available");
            return false;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            Console.WriteLine("[ContentIndexer] Content is empty, skipping");
            return false;
        }

        try
        {
            var metadataDict = new Dictionary<string, string>
            {
                ["type"] = metadata.Type,
                ["chat_id"] = metadata.ChatId.ToString(),
                ["user_id"] = metadata.UserId.ToString(),
                ["message_id"] = metadata.MessageId.ToString(),
                ["created_at"] = metadata.CreatedAt.ToString("O")
            };

            // Add optional fields
            if (!string.IsNullOrEmpty(metadata.FilePath))
            {
                metadataDict["file_path"] = metadata.FilePath;
            }
            if (!string.IsNullOrEmpty(metadata.MediaFileId))
            {
                metadataDict["media_file_id"] = metadata.MediaFileId;
            }

            await _memory.IndexAsync(documentId, content, metadataDict, ct);

            Console.WriteLine($"[ContentIndexer] Indexed: {documentId} ({metadata.Type}, {content.Length} chars)");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ContentIndexer] Index failed: {ex.Message}");
            return false;
        }
    }

    // Index a text message
    public async Task<bool> IndexTextMessageAsync(
        long messageId,
        long chatId,
        long userId,
        string text,
        DateTime createdAt,
        CancellationToken ct = default)
    {
        var documentId = $"msg_{chatId}_{messageId}";
        var metadata = new ContentMetadata
        {
            Type = "text",
            ChatId = chatId,
            UserId = userId,
            MessageId = messageId,
            CreatedAt = createdAt
        };

        return await IndexAsync(documentId, text, metadata, ct);
    }

    // Delete indexed content
    public async Task<bool> DeleteAsync(string documentId, CancellationToken ct = default)
    {
        if (_memory == null || !_memory.IsAvailable)
        {
            return false;
        }

        try
        {
            await _memory.DeleteAsync(documentId, ct);
            Console.WriteLine($"[ContentIndexer] Deleted: {documentId}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ContentIndexer] Delete failed: {ex.Message}");
            return false;
        }
    }
}
