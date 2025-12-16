namespace TelegramBotService;

// Input context for analyzers
public class AnalyzerContext
{
    // Common fields
    public string ContentType { get; set; } = "";  // "Text", "Photo", "Video", etc.

    // Text content
    public string Text { get; set; } = "";
    public string Caption { get; set; } = "";

    // Media content
    public string FileId { get; set; } = "";
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public string MimeType { get; set; } = "";

    // Optional: raw bytes for media (if downloaded)
    public byte[] FileData { get; set; } = Array.Empty<byte>();

    // Extension data for custom analyzers
    public Dictionary<string, object> Extra { get; set; } = new();
}

// Output result from analyzers
public class AnalyzerResult
{
    public bool Success { get; set; }
    public string Result { get; set; } = "";
    public string Error { get; set; } = "";
    public double ProcessingTimeMs { get; set; }

    // Structured data for programmatic access
    public Dictionary<string, object> Data { get; set; } = new();

    public static AnalyzerResult Ok(string result, Dictionary<string, object> data = null)
    {
        return new AnalyzerResult
        {
            Success = true,
            Result = result,
            Data = data ?? new Dictionary<string, object>()
        };
    }

    public static AnalyzerResult Fail(string error)
    {
        return new AnalyzerResult
        {
            Success = false,
            Error = error
        };
    }
}

// Base interface for all analyzers
public interface IAnalyzer
{
    // Unique name for this analyzer
    string Name { get; }

    // Description of what this analyzer does
    string Description { get; }

    // Content types this analyzer can handle (e.g., "Text", "Photo", "*" for all)
    string[] SupportedContentTypes { get; }

    // Priority for ordering (higher = runs first)
    int Priority { get; }

    // Perform analysis
    Task<AnalyzerResult> AnalyzeAsync(AnalyzerContext context, CancellationToken ct = default);
}

// Abstract base class with common functionality
public abstract class AnalyzerBase : IAnalyzer
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string[] SupportedContentTypes { get; }
    public virtual int Priority => 0;

    public abstract Task<AnalyzerResult> AnalyzeAsync(AnalyzerContext context, CancellationToken ct = default);

    protected bool CanHandle(string contentType)
    {
        if (SupportedContentTypes.Contains("*"))
            return true;
        return SupportedContentTypes.Contains(contentType);
    }
}
