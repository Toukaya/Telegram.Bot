using TelegramBotService;
using TelegramBotService.Services;
using TelegramBotService.TaskAI;
using TelegramBotService.TaskAI.Models;
using ForwardAnalyzerBot.Models;
using ForwardAnalyzerBot.Services;
using BotDatabase.Services;
using BotDatabase.Entities;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramMessage = Telegram.Bot.Types.Message;
using DbMessage = BotDatabase.Entities.Message;

namespace ForwardAnalyzerBot.Bot;

public class MessageHandler
{
    private const string Tag = "Handler";

    private readonly ITelegramBotClient _bot;
    private readonly TaskPool _taskPool;
    private readonly ScriptRunner _scriptRunner;
    private readonly AnalyzerService _analyzerService;
    private readonly BotDb _db;

    // New indexing services
    private readonly MediaProcessor _mediaProcessor;
    private readonly ContentIndexer _contentIndexer;
    private readonly SearchService _searchService;

    // TaskAI service
    private readonly ITaskAiService _taskAiService;

    private static readonly HashSet<string> MediaTypes = new()
    {
        "Photo", "Video", "Voice", "Audio", "VideoNote", "Sticker", "Document"
    };

    // Legacy constructor - uses shell scripts
    public MessageHandler(ITelegramBotClient bot, TaskPool taskPool, ScriptRunner scriptRunner, BotDb db)
    {
        _bot = bot;
        _taskPool = taskPool;
        _scriptRunner = scriptRunner;
        _db = db;
    }

    // New constructor - uses C# plugin system
    public MessageHandler(ITelegramBotClient bot, TaskPool taskPool, AnalyzerService analyzerService, BotDb db)
    {
        _bot = bot;
        _taskPool = taskPool;
        _analyzerService = analyzerService;
        _db = db;
    }

    // Full constructor with indexing services
    public MessageHandler(
        ITelegramBotClient bot,
        TaskPool taskPool,
        AnalyzerService analyzerService,
        BotDb db,
        MediaProcessor mediaProcessor,
        ContentIndexer contentIndexer,
        SearchService searchService)
    {
        _bot = bot;
        _taskPool = taskPool;
        _analyzerService = analyzerService;
        _db = db;
        _mediaProcessor = mediaProcessor;
        _contentIndexer = contentIndexer;
        _searchService = searchService;
    }

    // Full constructor with indexing services and TaskAI
    public MessageHandler(
        ITelegramBotClient bot,
        TaskPool taskPool,
        AnalyzerService analyzerService,
        BotDb db,
        MediaProcessor mediaProcessor,
        ContentIndexer contentIndexer,
        SearchService searchService,
        ITaskAiService taskAiService)
    {
        _bot = bot;
        _taskPool = taskPool;
        _analyzerService = analyzerService;
        _db = db;
        _mediaProcessor = mediaProcessor;
        _contentIndexer = contentIndexer;
        _searchService = searchService;
        _taskAiService = taskAiService;
    }

    public async Task HandleMessageAsync(TelegramMessage message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;

        // Handle /task command
        if (message.Text != null && message.Text.StartsWith("/task"))
        {
            await HandleTaskCommandAsync(message, ct);
            return;
        }

        // Handle /search command
        if (message.Text != null && message.Text.StartsWith("/search"))
        {
            await HandleSearchCommandAsync(message, ct);
            return;
        }

        // Check if this is a forwarded message
        if (message.ForwardOrigin == null)
        {
            // Not forwarded - try to index if it has content
            if (HasMediaContent(message) || !string.IsNullOrEmpty(message.Text))
            {
                await IndexNonForwardedMessageAsync(message, ct);
            }
            else
            {
                Logger.Debug(Tag, $"Non-forwarded message from chat {chatId}, skipping");
                await _bot.SendMessage(
                    chatId: chatId,
                    text: "Please forward a message to me for analysis, or send media/text to index.",
                    cancellationToken: ct
                );
            }
            return;
        }

        // Acknowledge receipt
        var pendingCount = _taskPool.PendingCount;
        var queueMessage = pendingCount > 0
            ? $"Added to analysis queue. Position: {pendingCount + 1}"
            : "Processing your message...";

        Logger.Info(Tag, $"Forwarded message queued (pending: {pendingCount})");

        await _bot.SendMessage(
            chatId: chatId,
            text: queueMessage,
            cancellationToken: ct
        );

        // Enqueue the analysis task
        await _taskPool.EnqueueAsync(async () =>
        {
            await ProcessForwardedMessageAsync(message, ct);
        });
    }

    // Handle /search command
    private async Task HandleSearchCommandAsync(TelegramMessage message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;

        if (_searchService == null || !_searchService.IsAvailable)
        {
            await _bot.SendMessage(chatId, "Search is not available.", cancellationToken: ct);
            return;
        }

        // Extract query from command
        var query = message.Text.Length > 7
            ? message.Text.Substring(7).Trim()
            : "";

        if (string.IsNullOrEmpty(query))
        {
            await _bot.SendMessage(chatId, "Usage: /search <query>", cancellationToken: ct);
            return;
        }

        Logger.Info(Tag, $"Search query: {query}");

        try
        {
            var options = new SearchOptions
            {
                ChatId = chatId,
                Limit = 10
            };

            var results = await _searchService.SearchAsync(query, options, ct);
            var response = _searchService.FormatResults(results, query);

            await _bot.SendMessage(chatId, response, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            Logger.Error(Tag, $"Search error: {ex.Message}", ex);
            await _bot.SendMessage(chatId, $"Search failed: {ex.Message}", cancellationToken: ct);
        }
    }

    // Handle /task command
    private async Task HandleTaskCommandAsync(TelegramMessage message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;

        if (_taskAiService == null || !_taskAiService.IsAvailable)
        {
            await _bot.SendMessage(chatId, "TaskAI is not available.", cancellationToken: ct);
            return;
        }

        // Parse subcommand: /task [gen|next|done|list|show] [args]
        var text = message.Text.Trim();
        var parts = text.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        var subCommand = parts.Length > 1 ? parts[1].ToLower() : "help";
        var args = parts.Length > 2 ? parts[2] : "";

        try
        {
            switch (subCommand)
            {
                case "gen":
                case "generate":
                    await HandleTaskGenAsync(chatId, args, ct);
                    break;
                case "next":
                    await HandleTaskNextAsync(chatId, ct);
                    break;
                case "done":
                    await HandleTaskDoneAsync(chatId, args, ct);
                    break;
                case "list":
                    await HandleTaskListAsync(chatId, ct);
                    break;
                case "show":
                    await HandleTaskShowAsync(chatId, ct);
                    break;
                case "clear":
                    await HandleTaskClearAsync(chatId, ct);
                    break;
                default:
                    await SendTaskHelpAsync(chatId, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(Tag, $"Task command error: {ex.Message}", ex);
            await _bot.SendMessage(chatId, $"Error: {ex.Message}", cancellationToken: ct);
        }
    }

    private async Task SendTaskHelpAsync(long chatId, CancellationToken ct)
    {
        var help = """
        TaskAI Commands:
        /task gen <description> - Generate task backlog from description
        /task next - Show next ready task
        /task done <task_id> - Mark task as done
        /task list - List all tasks with status
        /task show - Show full backlog YAML
        /task clear - Clear current backlog
        """;
        await _bot.SendMessage(chatId, help, cancellationToken: ct);
    }

    private async Task HandleTaskGenAsync(long chatId, string specification, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(specification))
        {
            await _bot.SendMessage(chatId, "Usage: /task gen <project description>", cancellationToken: ct);
            return;
        }

        await _bot.SendMessage(chatId, "Generating task backlog...", cancellationToken: ct);

        var result = await _taskAiService.GenerateBacklogAsync(specification, ct);

        if (!result.Success)
        {
            await _bot.SendMessage(chatId, $"Generation failed: {result.Error}", cancellationToken: ct);
            return;
        }

        // Save to database
        var yaml = _taskAiService.SerializeBacklog(result.Data);
        var backlogEntity = new TaskBacklog
        {
            ChatId = chatId,
            Project = result.Data.Project,
            BacklogYaml = yaml
        };
        await _db.TaskBacklogs.UpsertAsync(backlogEntity);

        // Format response
        var taskCount = result.Data.GetAllTasks().Count;
        var readyCount = result.Data.GetReadyTasks().Count;

        var response = $"""
        Backlog generated for: {result.Data.Project}
        Total tasks: {taskCount}
        Ready to start: {readyCount}

        Use /task next to see the first task.
        """;
        await _bot.SendMessage(chatId, response, cancellationToken: ct);
    }

    private async Task HandleTaskNextAsync(long chatId, CancellationToken ct)
    {
        var backlogEntity = await _db.TaskBacklogs.GetByChatIdAsync(chatId);
        if (backlogEntity == null)
        {
            await _bot.SendMessage(chatId, "No backlog found. Use /task gen to create one.", cancellationToken: ct);
            return;
        }

        var parseResult = _taskAiService.ParseBacklog(backlogEntity.BacklogYaml);
        if (!parseResult.Success)
        {
            await _bot.SendMessage(chatId, $"Failed to parse backlog: {parseResult.Error}", cancellationToken: ct);
            return;
        }

        var readyTasks = parseResult.Data.GetReadyTasks();
        if (readyTasks.Count == 0)
        {
            var allTasks = parseResult.Data.GetAllTasks();
            var doneCount = allTasks.Count(t => t.State == TaskState.Done);
            if (doneCount == allTasks.Count)
            {
                await _bot.SendMessage(chatId, "All tasks completed!", cancellationToken: ct);
            }
            else
            {
                await _bot.SendMessage(chatId, "No tasks ready. Some tasks may have unmet dependencies.", cancellationToken: ct);
            }
            return;
        }

        var task = readyTasks[0];
        var response = $"""
        Next Task: {task.Id}
        Title: {task.Title}

        Description:
        {task.Description}

        Deliverables:
        {string.Join("\n", task.GetDeliverables().Select(d => $"- {d}"))}

        Done when:
        {string.Join("\n", task.GetDoneWhenCriteria().Select(c => $"- {c}"))}

        Use /task done {task.Id} when complete.
        """;
        await _bot.SendMessage(chatId, response, cancellationToken: ct);
    }

    private async Task HandleTaskDoneAsync(long chatId, string taskId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            await _bot.SendMessage(chatId, "Usage: /task done <task_id>", cancellationToken: ct);
            return;
        }

        var backlogEntity = await _db.TaskBacklogs.GetByChatIdAsync(chatId);
        if (backlogEntity == null)
        {
            await _bot.SendMessage(chatId, "No backlog found. Use /task gen to create one.", cancellationToken: ct);
            return;
        }

        var parseResult = _taskAiService.ParseBacklog(backlogEntity.BacklogYaml);
        if (!parseResult.Success)
        {
            await _bot.SendMessage(chatId, $"Failed to parse backlog: {parseResult.Error}", cancellationToken: ct);
            return;
        }

        var backlog = parseResult.Data;
        if (!backlog.MarkTaskDone(taskId))
        {
            await _bot.SendMessage(chatId, $"Task '{taskId}' not found.", cancellationToken: ct);
            return;
        }

        // Save updated backlog
        backlogEntity.BacklogYaml = _taskAiService.SerializeBacklog(backlog);
        await _db.TaskBacklogs.UpsertAsync(backlogEntity);

        var allTasks = backlog.GetAllTasks();
        var doneCount = allTasks.Count(t => t.State == TaskState.Done);
        var readyTasks = backlog.GetReadyTasks();

        var response = $"""
        Task '{taskId}' marked as done.
        Progress: {doneCount}/{allTasks.Count}
        Ready tasks: {readyTasks.Count}
        """;
        await _bot.SendMessage(chatId, response, cancellationToken: ct);
    }

    private async Task HandleTaskListAsync(long chatId, CancellationToken ct)
    {
        var backlogEntity = await _db.TaskBacklogs.GetByChatIdAsync(chatId);
        if (backlogEntity == null)
        {
            await _bot.SendMessage(chatId, "No backlog found. Use /task gen to create one.", cancellationToken: ct);
            return;
        }

        var parseResult = _taskAiService.ParseBacklog(backlogEntity.BacklogYaml);
        if (!parseResult.Success)
        {
            await _bot.SendMessage(chatId, $"Failed to parse backlog: {parseResult.Error}", cancellationToken: ct);
            return;
        }

        var allTasks = parseResult.Data.GetAllTasks();
        var readyIds = new HashSet<string>(parseResult.Data.GetReadyTasks().Select(t => t.Id));

        var lines = new List<string> { $"Project: {parseResult.Data.Project}", "" };

        foreach (var task in allTasks)
        {
            var status = task.State == TaskState.Done ? "[DONE]" :
                         readyIds.Contains(task.Id) ? "[READY]" : "[BLOCKED]";
            lines.Add($"{status} {task.Id}: {task.Title}");
        }

        var response = string.Join("\n", lines);
        await _bot.SendMessage(chatId, response, cancellationToken: ct);
    }

    private async Task HandleTaskShowAsync(long chatId, CancellationToken ct)
    {
        var backlogEntity = await _db.TaskBacklogs.GetByChatIdAsync(chatId);
        if (backlogEntity == null)
        {
            await _bot.SendMessage(chatId, "No backlog found. Use /task gen to create one.", cancellationToken: ct);
            return;
        }

        var yaml = backlogEntity.BacklogYaml;
        if (yaml.Length > 4000)
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(yaml));
            stream.Position = 0;
            await _bot.SendDocument(
                chatId: chatId,
                document: InputFile.FromStream(stream, "backlog.yaml"),
                caption: "Full backlog attached.",
                cancellationToken: ct
            );
        }
        else
        {
            await _bot.SendMessage(chatId, $"```yaml\n{yaml}\n```", parseMode: ParseMode.Markdown, cancellationToken: ct);
        }
    }

    private async Task HandleTaskClearAsync(long chatId, CancellationToken ct)
    {
        var deleted = await _db.TaskBacklogs.DeleteAsync(chatId);
        if (deleted)
        {
            await _bot.SendMessage(chatId, "Backlog cleared.", cancellationToken: ct);
        }
        else
        {
            await _bot.SendMessage(chatId, "No backlog to clear.", cancellationToken: ct);
        }
    }

    // Index non-forwarded messages (media or text)
    private async Task IndexNonForwardedMessageAsync(TelegramMessage message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;

        try
        {
            // Store user and chat
            await StoreUserAndChatAsync(message);

            if (HasMediaContent(message))
            {
                // Process media
                if (_mediaProcessor == null)
                {
                    Logger.Debug(Tag, "Media processor not available");
                    return;
                }

                Logger.Info(Tag, "Processing media for indexing...");
                var result = await _mediaProcessor.ProcessAsync(_bot, message, ct);

                if (result.Success)
                {
                    // Save MediaFile to database
                    await SaveMediaFileAsync(message, result);

                    var status = result.IsIndexed
                        ? "Media processed and indexed"
                        : "Media processed (indexing skipped)";

                    await _bot.SendMessage(chatId, status, cancellationToken: ct);
                }
                else
                {
                    await _bot.SendMessage(chatId, $"Media processing failed: {result.Error}", cancellationToken: ct);
                }
            }
            else if (!string.IsNullOrEmpty(message.Text))
            {
                // Index text
                if (_contentIndexer == null || !_contentIndexer.IsAvailable)
                {
                    Logger.Debug(Tag, "Content indexer not available");
                    return;
                }

                var indexed = await _contentIndexer.IndexTextMessageAsync(
                    message.MessageId,
                    chatId,
                    message.From?.Id ?? 0,
                    message.Text,
                    message.Date,
                    ct);

                if (indexed)
                {
                    await _bot.SendMessage(chatId, "Text indexed", cancellationToken: ct);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(Tag, $"Indexing error: {ex.Message}", ex);
        }
    }

    // Save MediaFile record to database
    private async Task SaveMediaFileAsync(TelegramMessage message, MediaProcessResult result)
    {
        if (_db == null) return;

        var mediaFile = new MediaFile
        {
            Id = result.MediaFileId,
            TelegramFileId = ExtractFileId(message) ?? "",
            TelegramFileUniqueId = ExtractFileUniqueId(message) ?? "",
            ChatId = message.Chat.Id,
            UserId = message.From?.Id ?? 0,
            MessageId = message.MessageId,
            FileType = result.FileType,
            MimeType = result.MimeType,
            FileName = result.FileName,
            FileSize = result.FileSize,
            LocalPath = result.LocalPath,
            TextContent = result.TextContent,
            ConvertStatus = result.IsConverted ? MediaConvertStatus.Completed : MediaConvertStatus.Skipped,
            IsIndexed = result.IsIndexed,
            IndexedAt = result.IsIndexed ? DateTime.UtcNow : DateTime.MinValue,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _db.MediaFiles.Create(mediaFile);
        Logger.Debug(Tag, $"MediaFile saved: {mediaFile.Id}");
    }

    // Check if message has media content
    private bool HasMediaContent(TelegramMessage message)
    {
        return message.Photo != null ||
               message.Audio != null ||
               message.Voice != null ||
               message.Video != null ||
               message.VideoNote != null ||
               message.Document != null ||
               message.Sticker != null;
    }

    // Extract file ID from message
    private string ExtractFileId(TelegramMessage message)
    {
        if (message.Photo != null && message.Photo.Length > 0)
            return message.Photo[^1].FileId;
        if (message.Audio != null) return message.Audio.FileId;
        if (message.Voice != null) return message.Voice.FileId;
        if (message.Video != null) return message.Video.FileId;
        if (message.VideoNote != null) return message.VideoNote.FileId;
        if (message.Document != null) return message.Document.FileId;
        if (message.Sticker != null) return message.Sticker.FileId;
        return null;
    }

    // Extract file unique ID from message
    private string ExtractFileUniqueId(TelegramMessage message)
    {
        if (message.Photo != null && message.Photo.Length > 0)
            return message.Photo[^1].FileUniqueId;
        if (message.Audio != null) return message.Audio.FileUniqueId;
        if (message.Voice != null) return message.Voice.FileUniqueId;
        if (message.Video != null) return message.Video.FileUniqueId;
        if (message.VideoNote != null) return message.VideoNote.FileUniqueId;
        if (message.Document != null) return message.Document.FileUniqueId;
        if (message.Sticker != null) return message.Sticker.FileUniqueId;
        return null;
    }

    private async Task ProcessForwardedMessageAsync(TelegramMessage message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Extract forward information
            var result = ForwardInfoExtractor.Extract(message);
            Logger.Debug(Tag, $"Extracted forward info: type={result.Sender.Type}, sender={result.Sender.Name}");

            // Store user and chat info
            await StoreUserAndChatAsync(message);

            // Store message in database
            var dbMessage = await StoreMessageAsync(message, result);
            Logger.Debug(Tag, $"Message stored: id={dbMessage.Id}");

            // Use plugin system if available, otherwise fall back to scripts
            if (_analyzerService != null)
            {
                // Use C# plugin system
                Logger.Debug(Tag, "Running analysis via plugin system");
                var context = AnalyzerService.CreateContext(result.Content);
                result.Analysis = await _analyzerService.RunAnalysisAsync(context, ct);
            }
            else
            {
                // Fall back to shell scripts (legacy)
                bool isMedia = IsMediaContent(result.Content.Type);
                Logger.Debug(Tag, $"Running analysis via script (media={isMedia})");

                if (isMedia)
                {
                    result.Analysis = await _scriptRunner.RunMediaAnalysisAsync(
                        result.Content.Type,
                        result.Content.FileId,
                        result.Content.Caption
                    );
                }
                else
                {
                    var textContent = GetTextContent(result.Content);
                    result.Analysis = await _scriptRunner.RunTextAnalysisAsync(textContent);
                }
            }

            // Store analysis result
            await StoreAnalysisResultAsync(dbMessage.Id, result);

            sw.Stop();
            Logger.Info(Tag, $"Analysis complete: success={result.Analysis.Success}, time={sw.ElapsedMilliseconds}ms");

            // Send JSON result
            var json = result.ToJson();

            // Split message if too long (Telegram limit is 4096 chars)
            if (json.Length > 4000)
            {
                // Send as document
                Logger.Debug(Tag, $"Result too long ({json.Length} chars), sending as file");
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
                stream.Position = 0;

                await _bot.SendDocument(
                    chatId: chatId,
                    document: InputFile.FromStream(stream, "analysis_result.json"),
                    caption: "Analysis complete. Result attached as JSON file.",
                    cancellationToken: ct
                );
            }
            else
            {
                await _bot.SendMessage(
                    chatId: chatId,
                    text: $"```json\n{json}\n```",
                    parseMode: ParseMode.Markdown,
                    cancellationToken: ct
                );
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            Logger.Error(Tag, $"Error processing message after {sw.ElapsedMilliseconds}ms", ex);

            try
            {
                await _bot.SendMessage(
                    chatId: chatId,
                    text: $"Error processing message: {ex.Message}",
                    cancellationToken: ct
                );
            }
            catch
            {
                // Ignore send errors
            }
        }
    }

    private bool IsMediaContent(string contentType)
    {
        return MediaTypes.Contains(contentType);
    }

    private string GetTextContent(ContentInfo content)
    {
        if (!string.IsNullOrEmpty(content.Text))
        {
            return content.Text;
        }
        if (!string.IsNullOrEmpty(content.Caption))
        {
            return content.Caption;
        }
        return "";
    }

    private async Task StoreUserAndChatAsync(TelegramMessage message)
    {
        if (_db == null) return;

        // Store user
        if (message.From != null)
        {
            await _db.Users.GetOrCreate(
                message.From.Id,
                message.From.Username ?? "",
                message.From.FirstName ?? "",
                message.From.LastName ?? ""
            );
        }

        // Store chat
        await _db.Chats.GetOrCreate(
            message.Chat.Id,
            message.Chat.Type.ToString().ToLower(),
            message.Chat.Title ?? "",
            message.Chat.Username ?? ""
        );
    }

    private async Task<DbMessage> StoreMessageAsync(TelegramMessage message, ForwardAnalysisResult result)
    {
        if (_db == null) return new DbMessage();

        var dbMessage = new DbMessage
        {
            TelegramMessageId = message.MessageId,
            ChatId = message.Chat.Id,
            UserId = message.From != null ? message.From.Id : 0,
            Content = result.Content.Text ?? result.Content.Caption ?? "",
            ContentType = result.Content.Type.ToLower(),
            FileId = result.Content.FileId ?? "",
            SentAt = result.Source.OriginalDate != DateTime.MinValue
                ? result.Source.OriginalDate
                : message.Date
        };

        dbMessage = await _db.Messages.Store(dbMessage);

        // Store forward source if this is a forwarded message
        if (message.ForwardOrigin != null)
        {
            long originId = 0;
            long.TryParse(result.Sender.Id, out originId);

            var forwardSource = new ForwardSource
            {
                Id = dbMessage.Id,
                OriginType = result.Sender.Type.ToLower(),
                OriginId = originId,
                OriginName = result.Sender.Name ?? "",
                OriginUsername = result.Sender.Username ?? "",
                OriginalDate = result.Source.OriginalDate,
                MessageLink = result.Source.MessageLink ?? ""
            };

            dbMessage.ForwardSourceId = forwardSource.Id;
            await _db.SaveAsync();
        }

        return dbMessage;
    }

    private async Task StoreAnalysisResultAsync(int messageId, ForwardAnalysisResult result)
    {
        if (_db == null || messageId == 0) return;

        bool isMedia = IsMediaContent(result.Content.Type);
        var analysisResult = new AnalysisResult
        {
            MessageId = messageId,
            ScriptType = isMedia ? ScriptTypes.Media : ScriptTypes.Text,
            Status = result.Analysis.Success ? AnalysisStatus.Completed : AnalysisStatus.Failed,
            Result = result.Analysis.Result ?? "",
            Error = result.Analysis.Error ?? "",
            ExitCode = result.Analysis.Success ? 0 : 1,
            ExecutionTimeMs = (long)result.Analysis.ProcessingTimeMs,
            CompletedAt = DateTime.UtcNow
        };

        await _db.Analysis.Store(analysisResult);
    }
}
