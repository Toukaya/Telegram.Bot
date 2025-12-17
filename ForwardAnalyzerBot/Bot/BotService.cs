using ForwardAnalyzerBot.Services;
using BotDatabase.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ForwardAnalyzerBot.Bot;

public class BotService : IDisposable
{
    private const string Tag = "Bot";

    private readonly TelegramBotClient _bot;
    private readonly TaskPool _taskPool;
    private readonly ScriptRunner _scriptRunner;
    private readonly AnalyzerService _analyzerService;
    private readonly BotDb _db;
    private readonly MessageHandler _messageHandler;
    private readonly CancellationTokenSource _cts = new();
    private readonly bool _usePlugins;
    private bool _disposed;

    // Legacy constructor - uses shell scripts
    public BotService(string token, BotDb db, string textScriptPath, string mediaScriptPath, int concurrency = 1, int scriptTimeout = 30)
    {
        _bot = new TelegramBotClient(token);
        _db = db;
        _taskPool = new TaskPool(concurrency);
        _scriptRunner = new ScriptRunner(textScriptPath, mediaScriptPath, scriptTimeout);
        _messageHandler = new MessageHandler(_bot, _taskPool, _scriptRunner, _db);
        _usePlugins = false;
        Logger.Debug(Tag, "Initialized with shell scripts mode");
    }

    // New constructor - uses C# plugin system
    public BotService(string token, BotDb db, AnalyzerService analyzerService, int concurrency = 1)
    {
        _bot = new TelegramBotClient(token);
        _db = db;
        _taskPool = new TaskPool(concurrency);
        _analyzerService = analyzerService;
        _messageHandler = new MessageHandler(_bot, _taskPool, _analyzerService, _db);
        _usePlugins = true;
        Logger.Debug(Tag, "Initialized with plugin system mode");
    }

    public async Task StartAsync()
    {
        var me = await _bot.GetMe();
        Logger.Info(Tag, $"Bot started: @{me.Username} (ID: {me.Id})");

        if (_usePlugins)
        {
            Logger.Info(Tag, $"Using C# plugin system with {_analyzerService.Analyzers.Count} analyzers");
        }
        else
        {
            if (!_scriptRunner.IsTextScriptAvailable())
            {
                Logger.Warn(Tag, "Text analysis script not found");
            }

            if (!_scriptRunner.IsMediaScriptAvailable())
            {
                Logger.Warn(Tag, "Media analysis script not found");
            }
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

        Logger.Info(Tag, "Listening for messages...");
    }

    public async Task StopAsync()
    {
        Logger.Info(Tag, "Stopping...");

        _cts.Cancel();

        // Wait for pending tasks to complete
        await _taskPool.StopAsync();

        Logger.Info(Tag, "Stopped");
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message == null)
        {
            return;
        }

        var message = update.Message;
        var sender = message.From;
        var senderName = sender != null ? $"@{sender.Username}" : "Unknown";

        Logger.Info(Tag, $"Message from {senderName} in chat {message.Chat.Id}");

        try
        {
            await _messageHandler.HandleMessageAsync(message, ct);
        }
        catch (Exception ex)
        {
            Logger.Error(Tag, "Error handling update", ex);
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

        Logger.Debug(Tag, "Disposing resources");
        _cts.Cancel();
        _taskPool.Dispose();
        _analyzerService?.Dispose();
        _db?.Dispose();
        _cts.Dispose();
    }
}
