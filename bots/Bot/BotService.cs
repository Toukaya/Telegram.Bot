using ForwardAnalyzerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ForwardAnalyzerBot.Bot;

public class BotService : IDisposable
{
    private readonly TelegramBotClient _bot;
    private readonly TaskPool _taskPool;
    private readonly ScriptRunner _scriptRunner;
    private readonly MessageHandler _messageHandler;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public BotService(string token, string textScriptPath, string mediaScriptPath, int concurrency = 1, int scriptTimeout = 30)
    {
        _bot = new TelegramBotClient(token);
        _taskPool = new TaskPool(concurrency);
        _scriptRunner = new ScriptRunner(textScriptPath, mediaScriptPath, scriptTimeout);
        _messageHandler = new MessageHandler(_bot, _taskPool, _scriptRunner);
    }

    public async Task StartAsync()
    {
        var me = await _bot.GetMe();
        Console.WriteLine($"[BotService] Bot started: @{me.Username} (ID: {me.Id})");

        if (!_scriptRunner.IsTextScriptAvailable())
        {
            Console.WriteLine("[BotService] Warning: Text analysis script not found.");
        }

        if (!_scriptRunner.IsMediaScriptAvailable())
        {
            Console.WriteLine("[BotService] Warning: Media analysis script not found.");
        }

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

        Console.WriteLine("[BotService] Listening for messages...");
    }

    public async Task StopAsync()
    {
        Console.WriteLine("[BotService] Stopping...");

        _cts.Cancel();

        // Wait for pending tasks to complete
        await _taskPool.StopAsync();

        Console.WriteLine("[BotService] Stopped.");
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message == null)
        {
            return;
        }

        var message = update.Message;
        var sender = message.From;
        var senderName = sender != null ? sender.Username : "Unknown";

        Console.WriteLine($"[BotService] Received message from @{senderName} in chat {message.Chat.Id}");

        try
        {
            await _messageHandler.HandleMessageAsync(message, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BotService] Error handling update: {ex.Message}");
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
    {
        Console.WriteLine($"[BotService] Error: {exception.Message}");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _taskPool.Dispose();
        _cts.Dispose();
    }
}
