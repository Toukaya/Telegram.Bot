using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotService.Pipeline;
using TelegramBotService.Storage;
using TelegramBotService.MediaConverters;

namespace TelegramBotService.Services;

// Result of media processing
public class MediaProcessResult
{
    public bool Success { get; set; }
    public string MediaFileId { get; set; } = "";      // Generated ID for this media
    public string LocalPath { get; set; } = "";        // Path in storage
    public string TextContent { get; set; } = "";      // Converted text
    public bool IsConverted { get; set; }
    public bool IsIndexed { get; set; }
    public string Error { get; set; } = "";
    public List<string> Warnings { get; set; } = new();

    // Info for database record
    public string FileType { get; set; } = "";
    public string MimeType { get; set; } = "";
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }

    public static MediaProcessResult Fail(string error)
    {
        return new MediaProcessResult
        {
            Success = false,
            Error = error
        };
    }
}

// Service for processing media files (download -> store -> convert -> index)
public class MediaProcessor : IDisposable
{
    private readonly TelegramFileService _fileService;
    private readonly MediaProcessingPipeline _pipeline;
    private readonly ContentIndexer _indexer;
    private bool _disposed;

    public MediaProcessor(
        TelegramFileService fileService,
        MediaProcessingPipeline pipeline,
        ContentIndexer indexer)
    {
        _fileService = fileService;
        _pipeline = pipeline;
        _indexer = indexer;
    }

    // Process media from a Telegram message
    public async Task<MediaProcessResult> ProcessAsync(
        ITelegramBotClient bot,
        Message message,
        CancellationToken ct = default)
    {
        var result = new MediaProcessResult();
        string tempPath = null;

        try
        {
            // Extract file info from message
            var fileInfo = ExtractFileInfo(message);
            if (fileInfo == null)
            {
                return MediaProcessResult.Fail("No media found in message");
            }

            result.FileType = fileInfo.Type;
            result.MimeType = fileInfo.MimeType;
            result.FileName = fileInfo.FileName;
            result.FileSize = fileInfo.Size;

            Console.WriteLine($"[MediaProcessor] Processing: {fileInfo.Type}, {fileInfo.FileName}");

            // Step 1: Download from Telegram
            var downloadResult = await _fileService.DownloadAsync(bot, fileInfo.FileId, ct);
            if (!downloadResult.Success)
            {
                return MediaProcessResult.Fail($"Download failed: {downloadResult.Error}");
            }
            tempPath = downloadResult.LocalPath;

            // Step 2: Process through pipeline (store + convert)
            var mediaFileId = Guid.NewGuid().ToString("N");
            var pipelineInput = new MediaInput
            {
                TelegramFileId = fileInfo.FileId,
                TelegramFileUniqueId = fileInfo.UniqueId,
                ChatId = message.Chat.Id,
                UserId = message.From?.Id ?? 0,
                MessageId = message.MessageId,
                FileType = fileInfo.Type,
                MimeType = fileInfo.MimeType,
                FileName = fileInfo.FileName,
                FileSize = fileInfo.Size,
                LocalFilePath = tempPath
            };

            var pipelineResult = await _pipeline.ProcessAsync(pipelineInput, ct);

            result.MediaFileId = pipelineResult.MediaFileId;
            result.LocalPath = pipelineResult.LocalPath;
            result.TextContent = pipelineResult.TextContent;
            result.IsConverted = pipelineResult.IsConverted;
            result.Warnings.AddRange(pipelineResult.Warnings);

            if (!pipelineResult.Success)
            {
                return MediaProcessResult.Fail($"Pipeline failed: {pipelineResult.Error}");
            }

            // Step 3: Index to vector database (if we have text content)
            if (!string.IsNullOrEmpty(result.TextContent) && _indexer.IsAvailable)
            {
                var metadata = new ContentMetadata
                {
                    Type = fileInfo.Type,
                    ChatId = message.Chat.Id,
                    UserId = message.From?.Id ?? 0,
                    MessageId = message.MessageId,
                    FilePath = result.LocalPath,
                    MediaFileId = result.MediaFileId,
                    CreatedAt = message.Date
                };

                result.IsIndexed = await _indexer.IndexAsync(
                    result.MediaFileId,
                    result.TextContent,
                    metadata,
                    ct);

                if (!result.IsIndexed)
                {
                    result.Warnings.Add("Failed to index to vector database");
                }
            }
            else if (string.IsNullOrEmpty(result.TextContent))
            {
                result.Warnings.Add("No text content to index");
            }
            else if (!_indexer.IsAvailable)
            {
                result.Warnings.Add("Vector database not available");
            }

            result.Success = true;
            Console.WriteLine($"[MediaProcessor] Completed: {result.MediaFileId}, indexed={result.IsIndexed}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MediaProcessor] Error: {ex.Message}");
            return MediaProcessResult.Fail(ex.Message);
        }
        finally
        {
            // Clean up temp file
            if (!string.IsNullOrEmpty(tempPath))
            {
                _fileService.CleanupTempFile(tempPath);
            }
        }
    }

    // Extract file info from Telegram message
    private MediaFileInfo ExtractFileInfo(Message message)
    {
        // Photo (array of sizes, pick largest)
        if (message.Photo != null && message.Photo.Length > 0)
        {
            var photo = message.Photo[^1];  // Last = largest
            return new MediaFileInfo
            {
                FileId = photo.FileId,
                UniqueId = photo.FileUniqueId,
                Type = "photo",
                MimeType = "image/jpeg",
                FileName = "photo.jpg",
                Size = photo.FileSize ?? 0
            };
        }

        // Audio
        if (message.Audio != null)
        {
            return new MediaFileInfo
            {
                FileId = message.Audio.FileId,
                UniqueId = message.Audio.FileUniqueId,
                Type = "audio",
                MimeType = message.Audio.MimeType ?? "audio/mpeg",
                FileName = message.Audio.FileName ?? "audio.mp3",
                Size = message.Audio.FileSize ?? 0
            };
        }

        // Voice
        if (message.Voice != null)
        {
            return new MediaFileInfo
            {
                FileId = message.Voice.FileId,
                UniqueId = message.Voice.FileUniqueId,
                Type = "voice",
                MimeType = message.Voice.MimeType ?? "audio/ogg",
                FileName = "voice.ogg",
                Size = message.Voice.FileSize ?? 0
            };
        }

        // Video
        if (message.Video != null)
        {
            return new MediaFileInfo
            {
                FileId = message.Video.FileId,
                UniqueId = message.Video.FileUniqueId,
                Type = "video",
                MimeType = message.Video.MimeType ?? "video/mp4",
                FileName = message.Video.FileName ?? "video.mp4",
                Size = message.Video.FileSize ?? 0
            };
        }

        // VideoNote
        if (message.VideoNote != null)
        {
            return new MediaFileInfo
            {
                FileId = message.VideoNote.FileId,
                UniqueId = message.VideoNote.FileUniqueId,
                Type = "video_note",
                MimeType = "video/mp4",
                FileName = "video_note.mp4",
                Size = message.VideoNote.FileSize ?? 0
            };
        }

        // Document (general files)
        if (message.Document != null)
        {
            return new MediaFileInfo
            {
                FileId = message.Document.FileId,
                UniqueId = message.Document.FileUniqueId,
                Type = "document",
                MimeType = message.Document.MimeType ?? "application/octet-stream",
                FileName = message.Document.FileName ?? "document",
                Size = message.Document.FileSize ?? 0
            };
        }

        // Sticker
        if (message.Sticker != null)
        {
            return new MediaFileInfo
            {
                FileId = message.Sticker.FileId,
                UniqueId = message.Sticker.FileUniqueId,
                Type = "sticker",
                MimeType = message.Sticker.IsAnimated ? "application/tgs" : "image/webp",
                FileName = "sticker.webp",
                Size = message.Sticker.FileSize ?? 0
            };
        }

        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pipeline?.Dispose();
    }

    // Helper class for file info
    private class MediaFileInfo
    {
        public string FileId { get; set; } = "";
        public string UniqueId { get; set; } = "";
        public string Type { get; set; } = "";
        public string MimeType { get; set; } = "";
        public string FileName { get; set; } = "";
        public long Size { get; set; }
    }
}
