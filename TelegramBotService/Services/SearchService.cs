using TelegramBotService.Pipeline;

namespace TelegramBotService.Services;

// Search options
public class SearchOptions
{
    public long ChatId { get; set; }              // Filter by chat (0 = all)
    public string ContentType { get; set; } = ""; // Filter by type (empty = all)
    public int Limit { get; set; } = 10;
}

// Individual search result item
public class SearchResultItem
{
    public string DocumentId { get; set; } = "";
    public string ContentType { get; set; } = "";      // text, audio, image, video
    public string TextSnippet { get; set; } = "";      // Content snippet
    public double Relevance { get; set; }
    public DateTime CreatedAt { get; set; }
    public string FilePath { get; set; } = "";         // Media file path (optional)
    public string MediaFileId { get; set; } = "";      // MediaFile.Id (optional)
    public long MessageId { get; set; }
    public long ChatId { get; set; }
}

// Service for searching indexed content
public class SearchService
{
    private readonly IMemoryService _memory;

    public SearchService(IMemoryService memory)
    {
        _memory = memory;
    }

    // Check if search is available
    public bool IsAvailable => _memory != null && _memory.IsAvailable;

    // Search for content
    public async Task<List<SearchResultItem>> SearchAsync(
        string query,
        SearchOptions options = null,
        CancellationToken ct = default)
    {
        options = options ?? new SearchOptions();
        var results = new List<SearchResultItem>();

        if (_memory == null || !_memory.IsAvailable)
        {
            Console.WriteLine("[SearchService] Memory service not available");
            return results;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            Console.WriteLine("[SearchService] Query is empty");
            return results;
        }

        try
        {
            // Build filters
            Dictionary<string, string> filters = null;
            if (options.ChatId > 0 || !string.IsNullOrEmpty(options.ContentType))
            {
                filters = new Dictionary<string, string>();
                if (options.ChatId > 0)
                {
                    filters["chat_id"] = options.ChatId.ToString();
                }
                if (!string.IsNullOrEmpty(options.ContentType))
                {
                    filters["type"] = options.ContentType;
                }
            }

            // Search in memory
            var searchResults = await _memory.SearchAsync(query, options.Limit, filters, ct);

            Console.WriteLine($"[SearchService] Found {searchResults.Count} results for: {query}");

            // Convert to SearchResultItem
            foreach (var result in searchResults)
            {
                var item = new SearchResultItem
                {
                    DocumentId = result.DocumentId,
                    TextSnippet = TruncateText(result.Text, 200),
                    Relevance = result.Relevance
                };

                // Extract metadata
                if (result.Metadata != null)
                {
                    item.ContentType = result.Metadata.GetValueOrDefault("type", "text");
                    item.FilePath = result.Metadata.GetValueOrDefault("file_path", "");
                    item.MediaFileId = result.Metadata.GetValueOrDefault("media_file_id", "");

                    if (long.TryParse(result.Metadata.GetValueOrDefault("message_id", "0"), out var msgId))
                    {
                        item.MessageId = msgId;
                    }
                    if (long.TryParse(result.Metadata.GetValueOrDefault("chat_id", "0"), out var chatId))
                    {
                        item.ChatId = chatId;
                    }
                    if (DateTime.TryParse(result.Metadata.GetValueOrDefault("created_at", ""), out var createdAt))
                    {
                        item.CreatedAt = createdAt;
                    }
                }

                results.Add(item);
            }

            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SearchService] Search failed: {ex.Message}");
            return results;
        }
    }

    // Format search results for display
    public string FormatResults(List<SearchResultItem> results, string query)
    {
        if (results.Count == 0)
        {
            return $"No results found for: {query}";
        }

        var lines = new List<string>
        {
            $"Found {results.Count} result(s) for: {query}",
            ""
        };

        for (int i = 0; i < results.Count; i++)
        {
            var item = results[i];
            var typeLabel = GetTypeLabel(item.ContentType);
            var dateStr = item.CreatedAt != DateTime.MinValue
                ? item.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                : "Unknown date";

            lines.Add($"{i + 1}. [{typeLabel}] {dateStr}");
            lines.Add($"   \"{item.TextSnippet}\"");
            lines.Add($"   Relevance: {item.Relevance:F2}");
            lines.Add("");
        }

        return string.Join("\n", lines);
    }

    private string GetTypeLabel(string contentType)
    {
        return contentType switch
        {
            "audio" => "Audio",
            "voice" => "Voice",
            "video" => "Video",
            "video_note" => "Video",
            "photo" => "Photo",
            "text" => "Text",
            _ => "Other"
        };
    }

    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        // Clean up whitespace
        text = text.Replace("\n", " ").Replace("\r", "").Trim();

        if (text.Length <= maxLength)
        {
            return text;
        }

        return text.Substring(0, maxLength - 3) + "...";
    }
}
