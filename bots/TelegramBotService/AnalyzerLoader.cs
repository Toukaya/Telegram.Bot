using System.Reflection;
using System.Runtime.Loader;

namespace TelegramBotService;

// Custom AssemblyLoadContext for hot-reload support
public class AnalyzerLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public AnalyzerLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly Load(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }
        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }
        return IntPtr.Zero;
    }
}

// Manages loading and hot-reloading of analyzer plugins
public class AnalyzerLoader : IDisposable
{
    private readonly string _pluginsDirectory;
    private readonly object _lock = new();
    private FileSystemWatcher _watcher;
    private List<IAnalyzer> _analyzers = new();
    private List<AnalyzerLoadContext> _loadContexts = new();
    private bool _disposed;

    public event Action<string> OnReloaded;
    public event Action<string> OnError;

    public AnalyzerLoader(string pluginsDirectory)
    {
        _pluginsDirectory = pluginsDirectory;

        if (!Directory.Exists(_pluginsDirectory))
        {
            Directory.CreateDirectory(_pluginsDirectory);
        }
    }

    public IReadOnlyList<IAnalyzer> Analyzers
    {
        get
        {
            lock (_lock)
            {
                return _analyzers.ToList();
            }
        }
    }

    // Load all analyzers from the plugins directory
    public void LoadAll()
    {
        lock (_lock)
        {
            UnloadAllInternal();

            var dllFiles = Directory.GetFiles(_pluginsDirectory, "*.dll", SearchOption.AllDirectories);

            foreach (var dllPath in dllFiles)
            {
                try
                {
                    LoadPluginInternal(dllPath);
                }
                catch (Exception ex)
                {
                    OnError?.Invoke($"Failed to load {dllPath}: {ex.Message}");
                }
            }

            _analyzers = _analyzers.OrderByDescending(a => a.Priority).ToList();
        }
    }

    // Load built-in analyzers from the current assembly
    public void LoadBuiltIn()
    {
        lock (_lock)
        {
            var assembly = typeof(AnalyzerLoader).Assembly;
            LoadAnalyzersFromAssemblyInternal(assembly);
            _analyzers = _analyzers.OrderByDescending(a => a.Priority).ToList();
        }
    }

    // Load analyzers from a specific DLL file
    public void LoadPlugin(string dllPath)
    {
        lock (_lock)
        {
            LoadPluginInternal(dllPath);
            _analyzers = _analyzers.OrderByDescending(a => a.Priority).ToList();
        }
    }

    private void LoadPluginInternal(string dllPath)
    {
        var loadContext = new AnalyzerLoadContext(dllPath);
        _loadContexts.Add(loadContext);

        var assembly = loadContext.LoadFromAssemblyPath(dllPath);
        LoadAnalyzersFromAssemblyInternal(assembly);
    }

    // Load analyzers from a pre-loaded assembly (e.g., built-in analyzers)
    public void LoadFromAssembly(Assembly assembly)
    {
        lock (_lock)
        {
            LoadAnalyzersFromAssemblyInternal(assembly);
            _analyzers = _analyzers.OrderByDescending(a => a.Priority).ToList();
        }
    }

    private void LoadAnalyzersFromAssemblyInternal(Assembly assembly)
    {
        var analyzerTypes = assembly.GetTypes()
            .Where(t => typeof(IAnalyzer).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in analyzerTypes)
        {
            try
            {
                var analyzer = (IAnalyzer)Activator.CreateInstance(type);
                _analyzers.Add(analyzer);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to instantiate {type.Name}: {ex.Message}");
            }
        }
    }

    // Enable file watching for hot-reload
    public void EnableHotReload()
    {
        if (_watcher != null)
            return;

        _watcher = new FileSystemWatcher(_pluginsDirectory)
        {
            Filter = "*.dll",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnPluginFileChanged;
        _watcher.Created += OnPluginFileChanged;
        _watcher.Deleted += OnPluginFileChanged;
    }

    public void DisableHotReload()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    private DateTime _lastReloadTime = DateTime.MinValue;
    private readonly TimeSpan _reloadDebounce = TimeSpan.FromSeconds(2);

    private void OnPluginFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce rapid file changes
        var now = DateTime.Now;
        if (now - _lastReloadTime < _reloadDebounce)
            return;

        _lastReloadTime = now;

        // Reload on a background thread with a small delay
        Task.Run(async () =>
        {
            await Task.Delay(500);
            try
            {
                LoadAll();
                OnReloaded?.Invoke($"Plugins reloaded due to change in: {e.Name}");
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to reload plugins: {ex.Message}");
            }
        });
    }

    // Get analyzers that can handle a specific content type
    public IEnumerable<IAnalyzer> GetAnalyzersFor(string contentType)
    {
        lock (_lock)
        {
            return _analyzers
                .Where(a => a.SupportedContentTypes.Contains("*") || a.SupportedContentTypes.Contains(contentType))
                .ToList();
        }
    }

    // Run all applicable analyzers for a context
    public async Task<List<AnalyzerResult>> RunAnalyzersAsync(AnalyzerContext context, CancellationToken ct = default)
    {
        var results = new List<AnalyzerResult>();
        var analyzers = GetAnalyzersFor(context.ContentType);

        foreach (var analyzer in analyzers)
        {
            if (ct.IsCancellationRequested)
                break;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var result = await analyzer.AnalyzeAsync(context, ct);
                stopwatch.Stop();
                result.ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                result.Data["_analyzerName"] = analyzer.Name;
                results.Add(result);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                results.Add(new AnalyzerResult
                {
                    Success = false,
                    Error = $"{analyzer.Name}: {ex.Message}",
                    ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                    Data = new Dictionary<string, object> { ["_analyzerName"] = analyzer.Name }
                });
            }
        }

        return results;
    }

    // Run first applicable analyzer (similar to original ScriptRunner behavior)
    public async Task<AnalyzerResult> RunFirstAnalyzerAsync(AnalyzerContext context, CancellationToken ct = default)
    {
        var analyzers = GetAnalyzersFor(context.ContentType);
        var analyzer = analyzers.FirstOrDefault();

        if (analyzer == null)
        {
            return AnalyzerResult.Fail($"No analyzer found for content type: {context.ContentType}");
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = await analyzer.AnalyzeAsync(context, ct);
            stopwatch.Stop();
            result.ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new AnalyzerResult
            {
                Success = false,
                Error = $"{analyzer.Name}: {ex.Message}",
                ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds
            };
        }
    }

    private void UnloadAllInternal()
    {
        _analyzers.Clear();

        foreach (var context in _loadContexts)
        {
            context.Unload();
        }
        _loadContexts.Clear();

        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        DisableHotReload();

        lock (_lock)
        {
            UnloadAllInternal();
        }
    }
}
