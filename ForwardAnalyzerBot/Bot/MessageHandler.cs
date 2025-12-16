using TelegramBotService;
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
    private readonly ITelegramBotClient _bot;
    private readonly TaskPool _taskPool;
    private readonly ScriptRunner _scriptRunner;
    private readonly AnalyzerService _analyzerService;
    private readonly BotDb _db;

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

    public async Task HandleMessageAsync(TelegramMessage message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;

        // Check if this is a forwarded message
        if (message.ForwardOrigin == null)
        {
            await _bot.SendMessage(
                chatId: chatId,
                text: "Please forward a message to me for analysis.",
                cancellationToken: ct
            );
            return;
        }

        // Acknowledge receipt
        var pendingCount = _taskPool.PendingCount;
        var queueMessage = pendingCount > 0
            ? $"Added to analysis queue. Position: {pendingCount + 1}"
            : "Processing your message...";

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

    private async Task ProcessForwardedMessageAsync(TelegramMessage message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;

        try
        {
            // Extract forward information
            var result = ForwardInfoExtractor.Extract(message);

            // Store user and chat info
            await StoreUserAndChatAsync(message);

            // Store message in database
            var dbMessage = await StoreMessageAsync(message, result);

            // Use plugin system if available, otherwise fall back to scripts
            if (_analyzerService != null)
            {
                // Use C# plugin system
                var context = AnalyzerService.CreateContext(result.Content);
                result.Analysis = await _analyzerService.RunAnalysisAsync(context, ct);
            }
            else
            {
                // Fall back to shell scripts (legacy)
                bool isMedia = IsMediaContent(result.Content.Type);

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

            // Send JSON result
            var json = result.ToJson();

            // Split message if too long (Telegram limit is 4096 chars)
            if (json.Length > 4000)
            {
                // Send as document
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
            Console.WriteLine($"[MessageHandler] Error processing message: {ex.Message}");

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
