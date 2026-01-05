using ForwardAnalyzerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ForwardAnalyzerBot.Bot;

public class TranscriptionBotService : IDisposable
{
    private const string Tag = "TransBot";

    private readonly TelegramBotClient _bot;
    private readonly MessageTranscriber _transcriber;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public TranscriptionBotService(
        string token,
        string outputPath,
        string tempPath = "./temp",
        string whisperCliPath = null,
        string whisperModelPath = null)
    {
        _bot = new TelegramBotClient(token);
        _transcriber = new MessageTranscriber(
            _bot,
            outputPath,
            tempPath,
            whisperCliPath,
            whisperModelPath
        );
    }

    public async Task StartAsync()
    {
        var me = await _bot.GetMe();
        Logger.Info(Tag, $"Transcription Bot started: @{me.Username} (ID: {me.Id})");
        Logger.Info(Tag, "Forward messages to me, they will be transcribed and saved.");

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message },
            DropPendingUpdates = true
        };

        _bot.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: _cts.Token
        );

        Logger.Info(Tag, "Listening for messages...");
    }

    public async Task StopAsync()
    {
        Logger.Info(Tag, "Stopping...");
        _cts.Cancel();
        await _transcriber.StopAsync();
        Logger.Info(Tag, "Stopped");
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message == null) return;

        var message = update.Message;
        var sender = message.From;
        var senderName = sender != null ? $"@{sender.Username}" : "Unknown";

        Logger.Debug(Tag, $"Message from {senderName}");

        // Handle commands
        if (message.Text != null && message.Text.StartsWith("/"))
        {
            await HandleCommandAsync(message, ct);
            return;
        }

        // Queue the message for processing
        _transcriber.Enqueue(message);

        // Acknowledge receipt
        var queueCount = _transcriber.QueueCount;
        var response = queueCount > 1
            ? $"Added to queue ({queueCount} pending)"
            : "Processing...";

        try
        {
            await _bot.SendMessage(
                chatId: message.Chat.Id,
                text: response,
                cancellationToken: ct
            );
        }
        catch (Exception ex)
        {
            Logger.Error(Tag, $"Failed to send acknowledgment: {ex.Message}");
        }
    }

    private async Task HandleCommandAsync(Message message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;
        var command = message.Text.Split(' ')[0].ToLower();

        switch (command)
        {
            case "/start":
                await _bot.SendMessage(
                    chatId,
                    "Transcription Bot ready.\n\n" +
                    "Forward messages to me and I will:\n" +
                    "- Save text messages\n" +
                    "- Transcribe voice/audio messages\n" +
                    "- Record everything in order\n\n" +
                    "Commands:\n" +
                    "/status - Show queue status",
                    cancellationToken: ct
                );
                break;

            case "/status":
                await _bot.SendMessage(
                    chatId,
                    $"Queue: {_transcriber.QueueCount} messages pending",
                    cancellationToken: ct
                );
                break;

            default:
                await _bot.SendMessage(
                    chatId,
                    "Unknown command. Just forward messages to me.",
                    cancellationToken: ct
                );
                break;
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
    {
        Logger.Error(Tag, "Telegram error", exception);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _transcriber.Dispose();
        _cts.Dispose();
    }
}
