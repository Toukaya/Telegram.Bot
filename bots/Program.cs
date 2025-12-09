using ForwardAnalyzerBot.Bot;

class Program
{
    static async Task Main(string[] args)
    {
        // Configuration
        var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        var textScriptPath = Environment.GetEnvironmentVariable("TEXT_SCRIPT_PATH");
        var mediaScriptPath = Environment.GetEnvironmentVariable("MEDIA_SCRIPT_PATH");

        // Default script paths
        if (string.IsNullOrEmpty(textScriptPath))
        {
            textScriptPath = "./analyze_text.sh";
        }

        if (string.IsNullOrEmpty(mediaScriptPath))
        {
            mediaScriptPath = "./analyze_media.sh";
        }

        // Validate token
        if (string.IsNullOrEmpty(token))
        {
            Console.WriteLine("Error: TELEGRAM_BOT_TOKEN environment variable is not set.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  export TELEGRAM_BOT_TOKEN=your_bot_token");
            Console.WriteLine("  export TEXT_SCRIPT_PATH=./analyze_text.sh   # optional");
            Console.WriteLine("  export MEDIA_SCRIPT_PATH=./analyze_media.sh # optional");
            Console.WriteLine("  dotnet run");
            Environment.Exit(1);
            return;
        }

        Console.WriteLine("=== Forward Message Analyzer Bot ===");
        Console.WriteLine($"Text script:  {textScriptPath}");
        Console.WriteLine($"Media script: {mediaScriptPath}");
        Console.WriteLine();

        using var botService = new BotService(
            token: token,
            textScriptPath: textScriptPath,
            mediaScriptPath: mediaScriptPath,
            concurrency: 1,
            scriptTimeout: 30
        );

        // Handle graceful shutdown
        var shutdownEvent = new ManualResetEventSlim(false);

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Console.WriteLine();
            Console.WriteLine("Shutdown requested...");
            shutdownEvent.Set();
        };

        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            shutdownEvent.Set();
        };

        // Start the bot
        await botService.StartAsync();

        Console.WriteLine("Press Ctrl+C to stop the bot.");
        Console.WriteLine();

        // Wait for shutdown signal
        shutdownEvent.Wait();

        // Stop gracefully
        await botService.StopAsync();

        Console.WriteLine("Bot stopped. Goodbye!");
    }
}
