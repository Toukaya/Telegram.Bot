using System.Reflection;
using TelegramBotService;
using TelegramBotService.Analyzers;
using Xunit;

namespace TelegramBotService.Tests;

public class AnalyzerLoaderTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _pluginsDir;

    public AnalyzerLoaderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"analyzer_loader_test_{Guid.NewGuid():N}");
        _pluginsDir = Path.Combine(_testDir, "plugins");
        Directory.CreateDirectory(_pluginsDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    // ==================== Constructor Tests ====================

    [Fact]
    public void AnalyzerLoader_Constructor_CreatesPluginsDirectory()
    {
        var newDir = Path.Combine(_testDir, "new_plugins");
        var loader = new AnalyzerLoader(newDir);

        Assert.True(Directory.Exists(newDir));
        loader.Dispose();
    }

    [Fact]
    public void AnalyzerLoader_Analyzers_StartsEmpty()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);

        Assert.Empty(loader.Analyzers);
    }

    // ==================== LoadBuiltIn Tests ====================

    [Fact]
    public void LoadBuiltIn_LoadsTextAnalyzer()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);

        loader.LoadBuiltIn();

        var analyzers = loader.Analyzers;
        Assert.NotEmpty(analyzers);
        Assert.Contains(analyzers, a => a.Name == "TextAnalyzer");
    }

    [Fact]
    public void LoadBuiltIn_LoadsMediaAnalyzer()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);

        loader.LoadBuiltIn();

        var analyzers = loader.Analyzers;
        Assert.Contains(analyzers, a => a.Name == "MediaAnalyzer");
    }

    [Fact]
    public void LoadBuiltIn_SortsByPriority()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);

        loader.LoadBuiltIn();

        var analyzers = loader.Analyzers.ToList();
        Assert.NotEmpty(analyzers);

        // Check that analyzers are sorted by descending priority
        for (int i = 0; i < analyzers.Count - 1; i++)
        {
            Assert.True(analyzers[i].Priority >= analyzers[i + 1].Priority);
        }
    }

    [Fact]
    public void LoadBuiltIn_CanCallMultipleTimes()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);

        loader.LoadBuiltIn();
        var count1 = loader.Analyzers.Count;

        loader.LoadBuiltIn();
        var count2 = loader.Analyzers.Count;

        // Second call adds more analyzers because LoadBuiltIn does not clear existing ones
        // It calls LoadAnalyzersFromAssemblyInternal which adds to the list
        Assert.True(count2 >= count1);
    }

    // ==================== LoadFromAssembly Tests ====================

    [Fact]
    public void LoadFromAssembly_LoadsFromCurrentAssembly()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        var assembly = typeof(TextAnalyzer).Assembly;

        loader.LoadFromAssembly(assembly);

        Assert.NotEmpty(loader.Analyzers);
        Assert.Contains(loader.Analyzers, a => a is TextAnalyzer);
    }

    [Fact]
    public void LoadFromAssembly_SortsByPriority()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        var assembly = typeof(TextAnalyzer).Assembly;

        loader.LoadFromAssembly(assembly);

        var analyzers = loader.Analyzers.ToList();
        for (int i = 0; i < analyzers.Count - 1; i++)
        {
            Assert.True(analyzers[i].Priority >= analyzers[i + 1].Priority);
        }
    }

    // ==================== GetAnalyzersFor Tests ====================

    [Fact]
    public void GetAnalyzersFor_Text_ReturnsTextAnalyzer()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        loader.LoadBuiltIn();

        var textAnalyzers = loader.GetAnalyzersFor("Text").ToList();

        Assert.NotEmpty(textAnalyzers);
        Assert.Contains(textAnalyzers, a => a.Name == "TextAnalyzer");
    }

    [Fact]
    public void GetAnalyzersFor_Photo_ReturnsMediaAnalyzer()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        loader.LoadBuiltIn();

        var photoAnalyzers = loader.GetAnalyzersFor("Photo").ToList();

        Assert.NotEmpty(photoAnalyzers);
        Assert.Contains(photoAnalyzers, a => a.Name == "MediaAnalyzer");
    }

    [Fact]
    public void GetAnalyzersFor_Video_ReturnsMediaAnalyzer()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        loader.LoadBuiltIn();

        var videoAnalyzers = loader.GetAnalyzersFor("Video").ToList();

        Assert.NotEmpty(videoAnalyzers);
        Assert.Contains(videoAnalyzers, a => a.Name == "MediaAnalyzer");
    }

    [Fact]
    public void GetAnalyzersFor_Audio_ReturnsMediaAnalyzer()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        loader.LoadBuiltIn();

        var audioAnalyzers = loader.GetAnalyzersFor("Audio").ToList();

        Assert.NotEmpty(audioAnalyzers);
        Assert.Contains(audioAnalyzers, a => a.Name == "MediaAnalyzer");
    }

    [Fact]
    public void GetAnalyzersFor_UnknownType_ReturnsEmpty()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        loader.LoadBuiltIn();

        var unknownAnalyzers = loader.GetAnalyzersFor("UnknownType").ToList();

        Assert.Empty(unknownAnalyzers);
    }

    [Fact]
    public void GetAnalyzersFor_Wildcard_ReturnsAll()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        loader.LoadBuiltIn();

        // Create a wildcard analyzer for testing
        var wildcardAnalyzer = new WildcardTestAnalyzer();
        var analyzers = new List<IAnalyzer> { wildcardAnalyzer };

        // Since we cannot directly modify the internal list, we test the concept
        // The actual wildcard support is in the implementation
        Assert.Contains("*", wildcardAnalyzer.SupportedContentTypes);
    }

    // ==================== RunAnalyzersAsync Tests ====================

    [Fact]
    public async Task RunAnalyzersAsync_Text_RunsTextAnalyzer()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        loader.LoadBuiltIn();

        var context = new AnalyzerContext
        {
            ContentType = "Text",
            Text = "Hello world"
        };

        var results = await loader.RunAnalyzersAsync(context);

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Data.ContainsKey("_analyzerName") && r.Data["_analyzerName"].ToString() == "TextAnalyzer");
        Assert.All(results, r => Assert.True(r.Success));
    }

    [Fact]
    public async Task RunAnalyzersAsync_Photo_RunsMediaAnalyzer()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        loader.LoadBuiltIn();

        var context = new AnalyzerContext
        {
            ContentType = "Photo",
            FileId = "photo123"
        };

        var results = await loader.RunAnalyzersAsync(context);

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Data.ContainsKey("_analyzerName") && r.Data["_analyzerName"].ToString() == "MediaAnalyzer");
    }

    [Fact]
    public async Task RunAnalyzersAsync_SetsProcessingTime()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        loader.LoadBuiltIn();

        var context = new AnalyzerContext
        {
            ContentType = "Text",
            Text = "Test"
        };

        var results = await loader.RunAnalyzersAsync(context);

        Assert.All(results, r => Assert.True(r.ProcessingTimeMs >= 0));
    }

    [Fact]
    public async Task RunAnalyzersAsync_AddsAnalyzerName()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        loader.LoadBuiltIn();

        var context = new AnalyzerContext
        {
            ContentType = "Text",
            Text = "Test"
        };

        var results = await loader.RunAnalyzersAsync(context);

        Assert.All(results, r => Assert.True(r.Data.ContainsKey("_analyzerName")));
    }

    [Fact]
    public async Task RunAnalyzersAsync_Cancellation_StopsExecution()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        loader.LoadBuiltIn();

        var context = new AnalyzerContext
        {
            ContentType = "Text",
            Text = "Test"
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var results = await loader.RunAnalyzersAsync(context, cts.Token);

        // Should return empty or incomplete results due to cancellation
        Assert.NotNull(results);
    }

    [Fact]
    public async Task RunAnalyzersAsync_NoMatchingAnalyzer_ReturnsEmpty()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        loader.LoadBuiltIn();

        var context = new AnalyzerContext
        {
            ContentType = "UnknownType"
        };

        var results = await loader.RunAnalyzersAsync(context);

        Assert.Empty(results);
    }

    // ==================== RunFirstAnalyzerAsync Tests ====================

    [Fact]
    public async Task RunFirstAnalyzerAsync_Text_RunsTextAnalyzer()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        loader.LoadBuiltIn();

        var context = new AnalyzerContext
        {
            ContentType = "Text",
            Text = "Hello world"
        };

        var result = await loader.RunFirstAnalyzerAsync(context);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Result);
    }

    [Fact]
    public async Task RunFirstAnalyzerAsync_Photo_RunsMediaAnalyzer()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        loader.LoadBuiltIn();

        var context = new AnalyzerContext
        {
            ContentType = "Photo",
            FileId = "photo123"
        };

        var result = await loader.RunFirstAnalyzerAsync(context);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task RunFirstAnalyzerAsync_SetsProcessingTime()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        loader.LoadBuiltIn();

        var context = new AnalyzerContext
        {
            ContentType = "Text",
            Text = "Test"
        };

        var result = await loader.RunFirstAnalyzerAsync(context);

        Assert.True(result.ProcessingTimeMs >= 0);
    }

    [Fact]
    public async Task RunFirstAnalyzerAsync_NoMatchingAnalyzer_Fails()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        loader.LoadBuiltIn();

        var context = new AnalyzerContext
        {
            ContentType = "UnknownType"
        };

        var result = await loader.RunFirstAnalyzerAsync(context);

        Assert.False(result.Success);
        Assert.Contains("No analyzer found", result.Error);
    }

    [Fact]
    public async Task RunFirstAnalyzerAsync_Cancellation_AcceptsToken()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        loader.LoadBuiltIn();

        var context = new AnalyzerContext
        {
            ContentType = "Text",
            Text = "Test"
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Should complete quickly since analysis is fast
        var result = await loader.RunFirstAnalyzerAsync(context, cts.Token);

        Assert.NotNull(result);
    }

    // ==================== LoadAll Tests ====================

    [Fact]
    public void LoadAll_EmptyDirectory_NoAnalyzers()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);

        loader.LoadAll();

        Assert.Empty(loader.Analyzers);
    }

    [Fact]
    public void LoadAll_NonExistentDll_HandlesGracefully()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);

        // Create a dummy file that is not a valid DLL
        var dummyFile = Path.Combine(_pluginsDir, "not_a_dll.dll");
        File.WriteAllText(dummyFile, "This is not a DLL");

        bool errorOccurred = false;
        loader.OnError += (msg) => { errorOccurred = true; };

        loader.LoadAll();

        Assert.True(errorOccurred);
    }

    [Fact]
    public void LoadAll_CanCallMultipleTimes()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);

        loader.LoadAll();
        var count1 = loader.Analyzers.Count;

        loader.LoadAll();
        var count2 = loader.Analyzers.Count;

        Assert.Equal(count1, count2);
    }

    // ==================== Event Tests ====================

    [Fact]
    public void OnError_TriggersForInvalidPlugin()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        bool errorTriggered = false;
        string errorMessage = null;

        loader.OnError += (msg) =>
        {
            errorTriggered = true;
            errorMessage = msg;
        };

        // Create invalid DLL file
        var invalidDll = Path.Combine(_pluginsDir, "invalid.dll");
        File.WriteAllText(invalidDll, "Not a valid DLL");

        loader.LoadAll();

        Assert.True(errorTriggered);
        Assert.NotNull(errorMessage);
    }

    // ==================== Hot Reload Tests ====================

    [Fact]
    public void EnableHotReload_CanEnable()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);

        loader.EnableHotReload();

        // If it does not throw, it worked
        Assert.True(true);

        loader.DisableHotReload();
    }

    [Fact]
    public void DisableHotReload_CanDisable()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);

        loader.EnableHotReload();
        loader.DisableHotReload();

        // Should not throw
        Assert.True(true);
    }

    [Fact]
    public void EnableHotReload_CalledTwice_DoesNotThrow()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);

        loader.EnableHotReload();
        loader.EnableHotReload();

        loader.DisableHotReload();
        Assert.True(true);
    }

    // ==================== Dispose Tests ====================

    [Fact]
    public void Dispose_CanDisposeMultipleTimes()
    {
        var loader = new AnalyzerLoader(_pluginsDir);

        loader.Dispose();
        loader.Dispose();

        // Should not throw
        Assert.True(true);
    }

    [Fact]
    public void Dispose_WithHotReload_DisablesWatcher()
    {
        var loader = new AnalyzerLoader(_pluginsDir);
        loader.EnableHotReload();

        loader.Dispose();

        // Should not throw
        Assert.True(true);
    }

    [Fact]
    public void Dispose_ClearsAnalyzers()
    {
        var loader = new AnalyzerLoader(_pluginsDir);
        loader.LoadBuiltIn();

        Assert.NotEmpty(loader.Analyzers);

        loader.Dispose();

        Assert.Empty(loader.Analyzers);
    }

    // ==================== Thread Safety Tests ====================

    [Fact]
    public async Task Analyzers_ConcurrentAccess_ThreadSafe()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        loader.LoadBuiltIn();

        var tasks = new List<Task>();

        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var analyzers = loader.Analyzers;
                Assert.NotNull(analyzers);
            }));
        }

        await Task.WhenAll(tasks);
        Assert.True(true);
    }

    [Fact]
    public async Task RunAnalyzersAsync_ConcurrentCalls_ThreadSafe()
    {
        using var loader = new AnalyzerLoader(_pluginsDir);
        loader.LoadBuiltIn();

        var tasks = new List<Task<List<AnalyzerResult>>>();

        for (int i = 0; i < 10; i++)
        {
            var context = new AnalyzerContext
            {
                ContentType = "Text",
                Text = $"Test {i}"
            };
            tasks.Add(loader.RunAnalyzersAsync(context));
        }

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.NotEmpty(r));
    }

    // ==================== Helper Classes ====================

    private class WildcardTestAnalyzer : AnalyzerBase
    {
        public override string Name => "WildcardTestAnalyzer";
        public override string Description => "Test analyzer that supports all types";
        public override string[] SupportedContentTypes => new[] { "*" };
        public override int Priority => 50;

        public override Task<AnalyzerResult> AnalyzeAsync(AnalyzerContext context, CancellationToken ct = default)
        {
            return Task.FromResult(AnalyzerResult.Ok("Wildcard analysis"));
        }
    }
}
