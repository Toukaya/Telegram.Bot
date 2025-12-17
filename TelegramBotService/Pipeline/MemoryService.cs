namespace TelegramBotService.Pipeline;

// Null implementation - does nothing, used when memory service is disabled
public class NullMemoryService : IMemoryService
{
    public bool IsAvailable => false;

    public Task IndexAsync(string documentId, string text, Dictionary<string, string> metadata, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task<List<SearchResult>> SearchAsync(string query, int limit = 10, Dictionary<string, string> filters = null, CancellationToken ct = default)
    {
        return Task.FromResult(new List<SearchResult>());
    }

    public Task DeleteAsync(string documentId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}

// Simple in-memory implementation for testing/development
public class InMemoryService : IMemoryService
{
    private readonly Dictionary<string, (string Text, Dictionary<string, string> Metadata)> _documents = new();
    private readonly object _lock = new object();

    public bool IsAvailable => true;

    public Task IndexAsync(string documentId, string text, Dictionary<string, string> metadata, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _documents[documentId] = (text, metadata ?? new Dictionary<string, string>());
        }
        return Task.CompletedTask;
    }

    public Task<List<SearchResult>> SearchAsync(string query, int limit = 10, Dictionary<string, string> filters = null, CancellationToken ct = default)
    {
        var results = new List<SearchResult>();
        var queryLower = query.ToLower();

        lock (_lock)
        {
            foreach (var kvp in _documents)
            {
                // Simple text matching
                var textLower = kvp.Value.Text.ToLower();
                if (textLower.Contains(queryLower))
                {
                    // Check filters
                    if (filters != null)
                    {
                        bool match = true;
                        foreach (var filter in filters)
                        {
                            if (!kvp.Value.Metadata.TryGetValue(filter.Key, out var value) || value != filter.Value)
                            {
                                match = false;
                                break;
                            }
                        }
                        if (!match) continue;
                    }

                    // Calculate simple relevance score
                    var occurrences = CountOccurrences(textLower, queryLower);
                    var relevance = Math.Min(1.0, occurrences * 0.1);

                    results.Add(new SearchResult
                    {
                        DocumentId = kvp.Key,
                        Text = kvp.Value.Text,
                        Relevance = relevance,
                        Metadata = kvp.Value.Metadata
                    });
                }
            }
        }

        return Task.FromResult(results
            .OrderByDescending(r => r.Relevance)
            .Take(limit)
            .ToList());
    }

    public Task DeleteAsync(string documentId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _documents.Remove(documentId);
        }
        return Task.CompletedTask;
    }

    private int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int i = 0;
        while ((i = text.IndexOf(pattern, i)) != -1)
        {
            count++;
            i += pattern.Length;
        }
        return count;
    }
}

// Placeholder for future Kernel Memory integration
// To implement:
// 1. Install Microsoft.KernelMemory.Core
// 2. Implement this class using IKernelMemory
// 3. Configure embedding model (local or OpenAI)
// 4. Configure vector store (SQLite, Qdrant, etc.)
/*
public class KernelMemoryService : IMemoryService
{
    private readonly IKernelMemory _memory;

    public KernelMemoryService(IKernelMemory memory)
    {
        _memory = memory;
    }

    public bool IsAvailable => _memory != null;

    public async Task IndexAsync(string documentId, string text, Dictionary<string, string> metadata, CancellationToken ct = default)
    {
        var tags = new TagCollection();
        foreach (var kvp in metadata)
        {
            tags.Add(kvp.Key, kvp.Value);
        }

        await _memory.ImportTextAsync(text, documentId: documentId, tags: tags, cancellationToken: ct);
    }

    public async Task<List<SearchResult>> SearchAsync(string query, int limit = 10, Dictionary<string, string> filters = null, CancellationToken ct = default)
    {
        var searchFilters = filters != null
            ? filters.Select(f => MemoryFilters.ByTag(f.Key, f.Value)).ToArray()
            : null;

        var results = await _memory.SearchAsync(query, limit: limit, filters: searchFilters, cancellationToken: ct);

        return results.Results.Select(r => new SearchResult
        {
            DocumentId = r.DocumentId,
            Text = r.Partitions.FirstOrDefault()?.Text ?? "",
            Relevance = r.Partitions.FirstOrDefault()?.Relevance ?? 0,
            Metadata = r.Tags.ToDictionary(t => t.Key, t => t.Value.FirstOrDefault() ?? "")
        }).ToList();
    }

    public async Task DeleteAsync(string documentId, CancellationToken ct = default)
    {
        await _memory.DeleteDocumentAsync(documentId, cancellationToken: ct);
    }
}
*/
