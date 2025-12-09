using ForwardAnalyzerBot.Models;
using ForwardAnalyzerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ForwardAnalyzerBot.Bot;

public class MessageHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly TaskPool _taskPool;
    private readonly ScriptRunner _scriptRunner;

    private static readonly HashSet<string> MediaTypes = new()
    {
        "Photo", "Video", "Voice", "Audio", "VideoNote", "Sticker", "Document"
    };

    public MessageHandler(ITelegramBotClient bot, TaskPool taskPool, ScriptRunner scriptRunner)
    {
        _bot = bot;
        _taskPool = taskPool;
        _scriptRunner = scriptRunner;
    }

    public async Task HandleMessageAsync(Message message, CancellationToken ct)
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

    private async Task ProcessForwardedMessageAsync(Message message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;

        try
        {
            // Extract forward information
            var result = ForwardInfoExtractor.Extract(message);

            // Determine if this is media or text content
            bool isMedia = IsMediaContent(result.Content.Type);

            if (isMedia)
            {
                // Run media analysis script
                result.Analysis = await _scriptRunner.RunMediaAnalysisAsync(
                    result.Content.Type,
                    result.Content.FileId,
                    result.Content.Caption
                );
            }
            else
            {
                // Run text analysis script
                var textContent = GetTextContent(result.Content);
                result.Analysis = await _scriptRunner.RunTextAnalysisAsync(textContent);
            }

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
}
