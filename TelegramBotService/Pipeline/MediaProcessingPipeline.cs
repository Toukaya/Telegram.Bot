using TelegramBotService.Storage;
using TelegramBotService.MediaConverters;

namespace TelegramBotService.Pipeline;

// Configuration for the processing pipeline
public class PipelineConfig
{
    public bool EnableConversion { get; set; } = true;
    public bool EnableIndexing { get; set; } = false;  // Requires Kernel Memory setup
    public int MaxConcurrentProcessing { get; set; } = 2;
    public int RetryCount { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
}

// Input for processing a media file
public class MediaInput
{
    public string TelegramFileId { get; set; } = "";
    public string TelegramFileUniqueId { get; set; } = "";
    public long ChatId { get; set; }
    public long UserId { get; set; }
    public long MessageId { get; set; }
    public string FileType { get; set; } = "";         // audio/video/photo/voice/document
    public string MimeType { get; set; } = "";
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }

    // Either provide stream or local path
    public Stream FileStream { get; set; }
    public string LocalFilePath { get; set; } = "";
}

// Result of pipeline processing
public class PipelineResult
{
    public bool Success { get; set; }
    public string MediaFileId { get; set; } = "";      // ID in database
    public string LocalPath { get; set; } = "";        // Path in storage
    public string TextContent { get; set; } = "";      // Converted text (if any)
    public bool IsConverted { get; set; }
    public bool IsIndexed { get; set; }
    public string Error { get; set; } = "";
    public List<string> Warnings { get; set; } = new();

    public static PipelineResult Ok(string mediaFileId, string localPath)
    {
        return new PipelineResult
        {
            Success = true,
            MediaFileId = mediaFileId,
            LocalPath = localPath
        };
    }

    public static PipelineResult Fail(string error)
    {
        return new PipelineResult
        {
            Success = false,
            Error = error
        };
    }
}

// Orchestrates the media processing: Store -> Convert -> Index
public class MediaProcessingPipeline : IDisposable
{
    private readonly FileStorage _fileStorage;
    private readonly MediaConversionService _conversionService;
    private readonly IMemoryService _memoryService;  // Optional
    private readonly PipelineConfig _config;
    private readonly SemaphoreSlim _semaphore;
    private bool _disposed;

    public MediaProcessingPipeline(
        FileStorage fileStorage,
        MediaConversionService conversionService = null,
        IMemoryService memoryService = null,
        PipelineConfig config = null)
    {
        _fileStorage = fileStorage;
        _conversionService = conversionService;
        _memoryService = memoryService;
        _config = config ?? new PipelineConfig();
        _semaphore = new SemaphoreSlim(_config.MaxConcurrentProcessing);
    }

    // Process a media file through the full pipeline
    public async Task<PipelineResult> ProcessAsync(MediaInput input, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            return await ProcessInternalAsync(input, ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<PipelineResult> ProcessInternalAsync(MediaInput input, CancellationToken ct)
    {
        var result = new PipelineResult();
        var mediaFileId = Guid.NewGuid().ToString("N");
        string localPath = null;

        try
        {
            // Step 1: Store the file
            Console.WriteLine($"[Pipeline] Step 1: Storing file {input.FileName}...");
            var extension = GetExtension(input.FileName, input.MimeType, input.FileType);

            StorageResult storageResult;
            if (input.FileStream != null)
            {
                storageResult = await _fileStorage.StoreAsync(
                    input.FileStream,
                    input.FileType,
                    extension,
                    mediaFileId,
                    ct);
            }
            else if (!string.IsNullOrEmpty(input.LocalFilePath))
            {
                storageResult = await _fileStorage.CopyAsync(
                    input.LocalFilePath,
                    input.FileType,
                    extension,
                    mediaFileId,
                    ct);
            }
            else
            {
                return PipelineResult.Fail("No file data provided");
            }

            if (!storageResult.Success)
            {
                return PipelineResult.Fail($"Storage failed: {storageResult.Error}");
            }

            localPath = storageResult.LocalPath;
            result.MediaFileId = mediaFileId;
            result.LocalPath = localPath;
            Console.WriteLine($"[Pipeline] Stored: {localPath}");

            // Step 2: Convert to text (optional)
            if (_config.EnableConversion && _conversionService != null && ShouldConvert(input.FileType))
            {
                Console.WriteLine($"[Pipeline] Step 2: Converting to text...");
                var conversionResult = await ConvertWithRetryAsync(localPath, input.FileType, ct);

                if (conversionResult.Success)
                {
                    result.TextContent = conversionResult.Text;
                    result.IsConverted = true;
                    Console.WriteLine($"[Pipeline] Converted: {conversionResult.Text.Length} chars");
                }
                else
                {
                    result.Warnings.Add($"Conversion failed: {conversionResult.Error}");
                    Console.WriteLine($"[Pipeline] Conversion failed: {conversionResult.Error}");
                }
            }
            else if (!_config.EnableConversion)
            {
                result.Warnings.Add("Conversion disabled");
            }
            else if (_conversionService == null)
            {
                result.Warnings.Add("Conversion service not available");
            }

            // Step 3: Index to memory (optional)
            if (_config.EnableIndexing && _memoryService != null && !string.IsNullOrEmpty(result.TextContent))
            {
                Console.WriteLine($"[Pipeline] Step 3: Indexing to memory...");
                try
                {
                    await _memoryService.IndexAsync(
                        documentId: mediaFileId,
                        text: result.TextContent,
                        metadata: new Dictionary<string, string>
                        {
                            ["file_type"] = input.FileType,
                            ["file_path"] = localPath,
                            ["chat_id"] = input.ChatId.ToString(),
                            ["user_id"] = input.UserId.ToString(),
                            ["message_id"] = input.MessageId.ToString()
                        },
                        ct: ct);

                    result.IsIndexed = true;
                    Console.WriteLine($"[Pipeline] Indexed: {mediaFileId}");
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Indexing failed: {ex.Message}");
                    Console.WriteLine($"[Pipeline] Indexing failed: {ex.Message}");
                }
            }

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            return PipelineResult.Fail($"Pipeline error: {ex.Message}");
        }
    }

    private async Task<ConversionResult> ConvertWithRetryAsync(string filePath, string fileType, CancellationToken ct)
    {
        var converterType = GetConverterType(fileType);
        if (converterType == null)
        {
            return ConversionResult.Fail($"No converter for type: {fileType}");
        }

        var context = new ConversionContext
        {
            ContentType = converterType,
            FilePath = filePath,
            FileName = Path.GetFileName(filePath)
        };

        for (int i = 0; i < _config.RetryCount; i++)
        {
            try
            {
                var result = await _conversionService.ConvertAsync(context, ct);
                if (result.Success)
                {
                    return result;
                }

                if (i < _config.RetryCount - 1)
                {
                    await Task.Delay(_config.RetryDelayMs * (i + 1), ct);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (i == _config.RetryCount - 1)
                {
                    return ConversionResult.Fail(ex.Message);
                }
            }
        }

        return ConversionResult.Fail("Max retries exceeded");
    }

    private bool ShouldConvert(string fileType)
    {
        return fileType == "audio" || fileType == "voice" ||
               fileType == "video" || fileType == "video_note" ||
               fileType == "photo";
    }

    private string GetConverterType(string fileType)
    {
        switch (fileType)
        {
            case "audio":
            case "voice":
                return "audio";
            case "video":
            case "video_note":
                return "video";
            case "photo":
                return "photo";
            default:
                return null;
        }
    }

    private string GetExtension(string fileName, string mimeType, string fileType)
    {
        // Try to get from filename
        if (!string.IsNullOrEmpty(fileName))
        {
            var ext = Path.GetExtension(fileName);
            if (!string.IsNullOrEmpty(ext))
            {
                return ext;
            }
        }

        // Try to get from MIME type
        if (!string.IsNullOrEmpty(mimeType))
        {
            var mapping = new Dictionary<string, string>
            {
                ["audio/ogg"] = ".ogg",
                ["audio/mpeg"] = ".mp3",
                ["audio/mp4"] = ".m4a",
                ["audio/wav"] = ".wav",
                ["video/mp4"] = ".mp4",
                ["video/quicktime"] = ".mov",
                ["image/jpeg"] = ".jpg",
                ["image/png"] = ".png",
                ["image/webp"] = ".webp",
                ["application/pdf"] = ".pdf"
            };

            if (mapping.TryGetValue(mimeType.ToLower(), out var ext))
            {
                return ext;
            }
        }

        // Default by file type
        switch (fileType)
        {
            case "audio": return ".mp3";
            case "voice": return ".ogg";
            case "video": return ".mp4";
            case "video_note": return ".mp4";
            case "photo": return ".jpg";
            case "sticker": return ".webp";
            default: return ".bin";
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _semaphore.Dispose();
    }
}

// Interface for memory/semantic index service (Kernel Memory or similar)
public interface IMemoryService
{
    // Index text with metadata
    Task IndexAsync(
        string documentId,
        string text,
        Dictionary<string, string> metadata,
        CancellationToken ct = default);

    // Search for relevant documents
    Task<List<SearchResult>> SearchAsync(
        string query,
        int limit = 10,
        Dictionary<string, string> filters = null,
        CancellationToken ct = default);

    // Delete document from index
    Task DeleteAsync(string documentId, CancellationToken ct = default);

    // Check if service is available
    bool IsAvailable { get; }
}

// Search result from memory service
public class SearchResult
{
    public string DocumentId { get; set; } = "";
    public string Text { get; set; } = "";
    public double Relevance { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}
