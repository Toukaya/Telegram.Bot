using TelegramBotService;
using ForwardAnalyzerBot.Models;

namespace ForwardAnalyzerBot.Services;

// Adapter service that bridges the old AnalysisInfo model with the new plugin system
public class AnalyzerService : IDisposable
{
    private readonly AnalyzerLoader _loader;
    private readonly string _pluginsDirectory;
    private bool _disposed;

    public AnalyzerService(string pluginsDirectory = null)
    {
        _pluginsDirectory = pluginsDirectory ?? Path.Combine(AppContext.BaseDirectory, "plugins");
        _loader = new AnalyzerLoader(_pluginsDirectory);

        _loader.OnReloaded += msg => Console.WriteLine($"[AnalyzerService] {msg}");
        _loader.OnError += msg => Console.WriteLine($"[AnalyzerService] Error: {msg}");
    }

    public void Initialize(bool enableHotReload = true)
    {
        // Load external plugins first if directory exists
        if (Directory.Exists(_pluginsDirectory))
        {
            _loader.LoadAll();
            Console.WriteLine($"[AnalyzerService] Plugins directory: {_pluginsDirectory}");
        }

        // Load built-in analyzers from TelegramBotService assembly (after plugins, so they're not cleared)
        _loader.LoadBuiltIn();

        Console.WriteLine($"[AnalyzerService] Loaded {_loader.Analyzers.Count} analyzers:");
        foreach (var analyzer in _loader.Analyzers)
        {
            Console.WriteLine($"  - {analyzer.Name}: {analyzer.Description}");
        }

        if (enableHotReload)
        {
            _loader.EnableHotReload();
            Console.WriteLine("[AnalyzerService] Hot-reload enabled");
        }
    }

    public IReadOnlyList<IAnalyzer> Analyzers => _loader.Analyzers;

    public bool HasAnalyzerFor(string contentType)
    {
        return _loader.GetAnalyzersFor(contentType).Any();
    }

    // Run text analysis (compatible with old API)
    public async Task<AnalysisInfo> RunTextAnalysisAsync(string text)
    {
        var context = new AnalyzerContext
        {
            ContentType = "Text",
            Text = text
        };

        return await RunAnalysisAsync(context);
    }

    // Run media analysis (compatible with old API)
    public async Task<AnalysisInfo> RunMediaAnalysisAsync(string mediaType, string fileId, string caption)
    {
        var context = new AnalyzerContext
        {
            ContentType = mediaType,
            FileId = fileId,
            Caption = caption
        };

        return await RunAnalysisAsync(context);
    }

    // Run analysis with full context
    public async Task<AnalysisInfo> RunAnalysisAsync(AnalyzerContext context, CancellationToken ct = default)
    {
        var result = await _loader.RunFirstAnalyzerAsync(context, ct);
        return ConvertToAnalysisInfo(result);
    }

    // Run all applicable analyzers
    public async Task<List<AnalysisInfo>> RunAllAnalyzersAsync(AnalyzerContext context, CancellationToken ct = default)
    {
        var results = await _loader.RunAnalyzersAsync(context, ct);
        return results.Select(ConvertToAnalysisInfo).ToList();
    }

    // Create AnalyzerContext from ContentInfo
    public static AnalyzerContext CreateContext(ContentInfo content)
    {
        return new AnalyzerContext
        {
            ContentType = content.Type,
            Text = content.Text,
            Caption = content.Caption,
            FileId = content.FileId,
            FileName = content.FileName,
            FileSize = content.FileSize
        };
    }

    private AnalysisInfo ConvertToAnalysisInfo(AnalyzerResult result)
    {
        return new AnalysisInfo
        {
            Success = result.Success,
            Result = result.Result,
            Error = result.Error,
            ProcessingTimeMs = result.ProcessingTimeMs
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _loader.Dispose();
    }
}
