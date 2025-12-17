namespace TelegramBotService.MediaConverters;

// Configuration for media conversion service
public class MediaConversionConfig
{
    public string WhisperPath { get; set; } = "whisper";
    public string WhisperModel { get; set; } = "base";
    public string TesseractPath { get; set; } = "tesseract";
    public string TesseractLanguages { get; set; } = "eng+chi_sim+jpn";
    public string FfmpegPath { get; set; } = "ffmpeg";
    public int TimeoutSeconds { get; set; } = 300;
}

// Service that manages media-to-text conversion
public class MediaConversionService : IDisposable
{
    private readonly List<IMediaConverter> _converters;
    private readonly MediaConversionConfig _config;
    private bool _disposed;

    public MediaConversionService(MediaConversionConfig config = null)
    {
        _config = config ?? new MediaConversionConfig();
        _converters = new List<IMediaConverter>();

        InitializeConverters();
    }

    private void InitializeConverters()
    {
        // Initialize built-in converters
        _converters.Add(new AudioConverter(
            _config.WhisperPath,
            _config.WhisperModel,
            _config.TimeoutSeconds));

        _converters.Add(new ImageConverter(
            _config.TesseractPath,
            _config.TesseractLanguages,
            _config.TimeoutSeconds / 5));  // Images should be faster

        _converters.Add(new VideoConverter(
            _config.FfmpegPath,
            _config.WhisperPath,
            _config.WhisperModel,
            _config.TimeoutSeconds * 2));  // Videos may take longer
    }

    // Get status of all converters
    public Dictionary<string, bool> GetConverterStatus()
    {
        return _converters.ToDictionary(c => c.Name, c => c.IsAvailable);
    }

    // Check if any converter is available for the given content type
    public bool CanConvert(string contentType)
    {
        return _converters.Any(c => c.IsAvailable && c.SupportedContentTypes.Contains(contentType));
    }

    // Get available converters for a content type
    public IEnumerable<IMediaConverter> GetConverters(string contentType)
    {
        return _converters
            .Where(c => c.IsAvailable && c.SupportedContentTypes.Contains(contentType))
            .OrderByDescending(c => c.Priority);
    }

    // Convert media to text using the best available converter
    public async Task<ConversionResult> ConvertAsync(ConversionContext context, CancellationToken ct = default)
    {
        var converters = GetConverters(context.ContentType).ToList();

        if (converters.Count == 0)
        {
            return ConversionResult.Unavailable($"No converter available for content type: {context.ContentType}");
        }

        // Try converters in priority order
        foreach (var converter in converters)
        {
            try
            {
                var result = await converter.ConvertAsync(context, ct);
                if (result.Success)
                {
                    result.Metadata["converter"] = converter.Name;
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Log and try next converter
                Console.WriteLine($"[MediaConversion] {converter.Name} failed: {ex.Message}");
            }
        }

        return ConversionResult.Fail($"All converters failed for content type: {context.ContentType}");
    }

    // Batch convert multiple files
    public async Task<List<(string FilePath, ConversionResult Result)>> ConvertBatchAsync(
        IEnumerable<ConversionContext> contexts,
        int maxConcurrency = 2,
        CancellationToken ct = default)
    {
        var results = new List<(string FilePath, ConversionResult Result)>();
        var semaphore = new SemaphoreSlim(maxConcurrency);

        var tasks = contexts.Select(async context =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var result = await ConvertAsync(context, ct);
                return (context.FilePath, result);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var completedTasks = await Task.WhenAll(tasks);
        results.AddRange(completedTasks);

        return results;
    }

    // Add custom converter
    public void AddConverter(IMediaConverter converter)
    {
        _converters.Add(converter);
    }

    // Print service status
    public void PrintStatus()
    {
        Console.WriteLine("[MediaConversion] Service Status:");
        foreach (var converter in _converters)
        {
            var status = converter.IsAvailable ? "OK" : "NOT AVAILABLE";
            Console.WriteLine($"  - {converter.Name}: {status} (types: {string.Join(", ", converter.SupportedContentTypes)})");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var converter in _converters)
        {
            if (converter is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _converters.Clear();
    }
}
