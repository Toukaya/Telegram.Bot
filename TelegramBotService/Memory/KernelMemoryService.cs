using Microsoft.KernelMemory;
using TelegramBotService.Pipeline;
using TelegramBotService.Configuration;

namespace TelegramBotService.Memory;

// Kernel Memory service implementation using SimpleVectorDb (file-based)
public class KernelMemoryService : IMemoryService, IDisposable
{
    private readonly MemoryServerless _memory;
    private readonly MemoryConfig _config;
    private bool _isAvailable;
    private bool _disposed;

    public bool IsAvailable => _isAvailable;

    public KernelMemoryService(MemoryConfig config)
    {
        _config = config;
        _isAvailable = false;

        if (!config.Enabled)
        {
            Console.WriteLine("[KernelMemory] Service disabled in configuration");
            return;
        }

        try
        {
            _memory = CreateMemory(config);
            _isAvailable = _memory != null;
            if (_isAvailable)
            {
                Console.WriteLine("[KernelMemory] Service initialized successfully");
            }
            else
            {
                Console.WriteLine("[KernelMemory] Service initialization returned null");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[KernelMemory] Failed to initialize: {ex.Message}");
            Console.WriteLine($"[KernelMemory] Stack trace: {ex.StackTrace}");
            _isAvailable = false;
        }
    }

    private MemoryServerless CreateMemory(MemoryConfig config)
    {
        var builder = new KernelMemoryBuilder();

        // Configure storage path for SimpleVectorDb and SimpleFileStorage
        var storagePath = Path.GetDirectoryName(config.SqliteStorePath);
        if (string.IsNullOrEmpty(storagePath))
        {
            storagePath = ".";
        }
        storagePath = Path.GetFullPath(storagePath);
        Directory.CreateDirectory(storagePath);

        Console.WriteLine($"[KernelMemory] Storage path: {storagePath}");

        // Use SimpleVectorDb (file-based, no external dependencies)
        builder.WithSimpleVectorDb(new Microsoft.KernelMemory.MemoryStorage.DevTools.SimpleVectorDbConfig
        {
            StorageType = Microsoft.KernelMemory.FileSystem.DevTools.FileSystemTypes.Disk,
            Directory = Path.Combine(storagePath, "vectors")
        });

        // Use SimpleFileStorage for document storage
        builder.WithSimpleFileStorage(new Microsoft.KernelMemory.DocumentStorage.DevTools.SimpleFileStorageConfig
        {
            StorageType = Microsoft.KernelMemory.FileSystem.DevTools.FileSystemTypes.Disk,
            Directory = Path.Combine(storagePath, "files")
        });

        // Configure embedding model
        ConfigureEmbedding(builder, config);

        // Configure text generation (optional, for answers)
        ConfigureTextGeneration(builder, config);

        return builder.Build<MemoryServerless>();
    }

    private void ConfigureEmbedding(IKernelMemoryBuilder builder, MemoryConfig config)
    {
        switch (config.EmbeddingProvider.ToLower())
        {
            case "openai":
                // To use OpenAI embeddings, add package: Microsoft.KernelMemory.AI.OpenAI
                // Then uncomment and configure:
                // builder.WithOpenAITextEmbeddingGeneration(new OpenAIConfig { ... });
                Console.WriteLine("[KernelMemory] OpenAI embeddings require Microsoft.KernelMemory.AI.OpenAI package");
                Console.WriteLine("[KernelMemory] Falling back to local embedding");
                goto case "local";

            case "local":
            default:
                // Use simple text-based matching (no vector embeddings)
                // This is limited but works without external dependencies
                Console.WriteLine("[KernelMemory] Using local text-based matching");
                break;
        }
    }

    private void ConfigureTextGeneration(IKernelMemoryBuilder builder, MemoryConfig config)
    {
        // Text generation is optional - only needed for RAG answers
        // For now, we'll skip it as we mainly need indexing and search
    }

    public async Task IndexAsync(
        string documentId,
        string text,
        Dictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        if (!_isAvailable || _memory == null)
        {
            throw new InvalidOperationException("Kernel Memory service is not available");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            Console.WriteLine($"[KernelMemory] Skipping empty text for document: {documentId}");
            return;
        }

        try
        {
            // Convert metadata to TagCollection
            var tags = new TagCollection();
            if (metadata != null)
            {
                foreach (var (key, value) in metadata)
                {
                    tags.Add(key, value);
                }
            }

            // Import text to memory
            await _memory.ImportTextAsync(
                text: text,
                documentId: documentId,
                tags: tags,
                cancellationToken: ct);

            Console.WriteLine($"[KernelMemory] Indexed document: {documentId} ({text.Length} chars)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[KernelMemory] Index failed for {documentId}: {ex.Message}");
            throw;
        }
    }

    public async Task<List<Pipeline.SearchResult>> SearchAsync(
        string query,
        int limit = 10,
        Dictionary<string, string> filters = null,
        CancellationToken ct = default)
    {
        if (!_isAvailable || _memory == null)
        {
            throw new InvalidOperationException("Kernel Memory service is not available");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<Pipeline.SearchResult>();
        }

        try
        {
            // Build filter from metadata
            MemoryFilter filter = null;
            if (filters != null && filters.Count > 0)
            {
                filter = new MemoryFilter();
                foreach (var (key, value) in filters)
                {
                    filter.Add(key, value);
                }
            }

            // Execute search
            var searchResult = await _memory.SearchAsync(
                query: query,
                limit: limit,
                filter: filter,
                cancellationToken: ct);

            // Convert results
            var results = new List<Pipeline.SearchResult>();
            foreach (var citation in searchResult.Results)
            {
                foreach (var partition in citation.Partitions)
                {
                    var meta = new Dictionary<string, string>();
                    if (partition.Tags != null)
                    {
                        foreach (var tag in partition.Tags)
                        {
                            meta[tag.Key] = string.Join(",", tag.Value);
                        }
                    }

                    results.Add(new Pipeline.SearchResult
                    {
                        DocumentId = citation.DocumentId,
                        Text = partition.Text,
                        Relevance = partition.Relevance,
                        Metadata = meta
                    });
                }
            }

            Console.WriteLine($"[KernelMemory] Search for '{query}' returned {results.Count} results");
            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[KernelMemory] Search failed: {ex.Message}");
            throw;
        }
    }

    public async Task DeleteAsync(string documentId, CancellationToken ct = default)
    {
        if (!_isAvailable || _memory == null)
        {
            throw new InvalidOperationException("Kernel Memory service is not available");
        }

        try
        {
            await _memory.DeleteDocumentAsync(documentId, cancellationToken: ct);
            Console.WriteLine($"[KernelMemory] Deleted document: {documentId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[KernelMemory] Delete failed for {documentId}: {ex.Message}");
            throw;
        }
    }

    // Check if a document exists
    public async Task<bool> DocumentExistsAsync(string documentId, CancellationToken ct = default)
    {
        if (!_isAvailable || _memory == null)
        {
            return false;
        }

        try
        {
            var status = await _memory.IsDocumentReadyAsync(documentId, cancellationToken: ct);
            return status;
        }
        catch
        {
            return false;
        }
    }

    // Get service status info
    public Dictionary<string, string> GetStatus()
    {
        return new Dictionary<string, string>
        {
            ["available"] = _isAvailable.ToString(),
            ["backend"] = _config.Backend,
            ["embedding_provider"] = _config.EmbeddingProvider,
            ["storage_path"] = _config.SqliteStorePath
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_memory != null)
        {
            // MemoryServerless doesn't implement IDisposable directly
            // but we should clean up any resources if needed
        }

        Console.WriteLine("[KernelMemory] Service disposed");
    }
}
