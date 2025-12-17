namespace TelegramBotService.MediaConverters;

// Result of media-to-text conversion
public class ConversionResult
{
    public bool Success { get; set; }
    public string Text { get; set; } = "";
    public string Error { get; set; } = "";
    public double ProcessingTimeMs { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();

    public static ConversionResult Ok(string text, Dictionary<string, object> metadata = null)
    {
        return new ConversionResult
        {
            Success = true,
            Text = text,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    public static ConversionResult Fail(string error)
    {
        return new ConversionResult
        {
            Success = false,
            Error = error
        };
    }

    public static ConversionResult Unavailable(string reason = "Conversion service not available")
    {
        return new ConversionResult
        {
            Success = false,
            Error = reason
        };
    }
}

// Input context for converters
public class ConversionContext
{
    public string ContentType { get; set; } = "";      // photo, audio, voice, video, video_note, document
    public string FilePath { get; set; } = "";         // Local file path
    public byte[] FileData { get; set; } = Array.Empty<byte>();  // File bytes (if loaded in memory)
    public string MimeType { get; set; } = "";
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public Dictionary<string, object> Extra { get; set; } = new();
}

// Interface for media-to-text converters
public interface IMediaConverter
{
    // Unique identifier for this converter
    string Name { get; }

    // Content types this converter can handle
    string[] SupportedContentTypes { get; }

    // Check if the converter service is available
    bool IsAvailable { get; }

    // Priority (higher = preferred)
    int Priority { get; }

    // Convert media to text
    Task<ConversionResult> ConvertAsync(ConversionContext context, CancellationToken ct = default);
}

// Base class for converters
public abstract class MediaConverterBase : IMediaConverter
{
    public abstract string Name { get; }
    public abstract string[] SupportedContentTypes { get; }
    public abstract bool IsAvailable { get; }
    public virtual int Priority => 0;

    public abstract Task<ConversionResult> ConvertAsync(ConversionContext context, CancellationToken ct = default);

    protected bool CanHandle(string contentType)
    {
        if (SupportedContentTypes.Contains("*"))
            return true;
        return SupportedContentTypes.Contains(contentType);
    }
}
