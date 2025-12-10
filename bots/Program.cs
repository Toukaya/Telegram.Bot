using ForwardAnalyzerBot.Bot;
using ForwardAnalyzerBot.Services;

class Program
{
    static async Task Main(string[] args)
    {
        // Configuration
        var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        var usePlugins = Environment.GetEnvironmentVariable("USE_PLUGINS")?.ToLower() == "true";
        var pluginsPath = Environment.GetEnvironmentVariable("PLUGINS_PATH");

        // Legacy script paths (only used if USE_PLUGINS != true)
        var textScriptPath = Environment.GetEnvironmentVariable("TEXT_SCRIPT_PATH") ?? "./analyze_text.sh";
        var mediaScriptPath = Environment.GetEnvironmentVariable("MEDIA_SCRIPT_PATH") ?? "./analyze_media.sh";

        // Validate token
        if (string.IsNullOrEmpty(token))
        {
            Console.WriteLine("Error: TELEGRAM_BOT_TOKEN environment variable is not set.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  export TELEGRAM_BOT_TOKEN=your_bot_token");
            Console.WriteLine();
            Console.WriteLine("  # Option 1: Use C# plugin system (recommended)");
            Console.WriteLine("  export USE_PLUGINS=true");
            Console.WriteLine("  export PLUGINS_PATH=./plugins              # optional");
            Console.WriteLine();
            Console.WriteLine("  # Option 2: Use legacy shell scripts");
            Console.WriteLine("  export TEXT_SCRIPT_PATH=./analyze_text.sh   # optional");
            Console.WriteLine("  export MEDIA_SCRIPT_PATH=./analyze_media.sh # optional");
            Console.WriteLine();
            Console.WriteLine("  dotnet run");
            Environment.Exit(1);
            return;
        }

        Console.WriteLine("=== Forward Message Analyzer Bot ===");

        BotService botService;

        if (usePlugins)
        {
            Console.WriteLine("Mode: C# Plugin System");
            Console.WriteLine();

            var analyzerService = new AnalyzerService(pluginsPath);
            analyzerService.Initialize(enableHotReload: true);
            Console.WriteLine();

            botService = new BotService(
                token: token,
                analyzerService: analyzerService,
                concurrency: 1
            );
        }
        else
        {
            Console.WriteLine("Mode: Legacy Shell Scripts");
            Console.WriteLine($"Text script:  {textScriptPath}");
            Console.WriteLine($"Media script: {mediaScriptPath}");
            Console.WriteLine();

            botService = new BotService(
                token: token,
                textScriptPath: textScriptPath,
                mediaScriptPath: mediaScriptPath,
                concurrency: 1,
                scriptTimeout: 30
            );
        }

        using (botService)
        {
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
}
