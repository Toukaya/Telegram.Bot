using ForwardAnalyzerBot.Bot;
using ForwardAnalyzerBot.Services;
using BotDatabase.Services;
using TelegramBotService.Pipeline;
using TelegramBotService.AI;
using TelegramBotService.TaskAI;

class Program
{
    static async Task Main(string[] args)
    {
        // Check for transcription mode
        var transcribeModeEnv = Environment.GetEnvironmentVariable("TRANSCRIBE_MODE");
        var transcribeMode = transcribeModeEnv != null && transcribeModeEnv.ToLower() == "true";
        if (transcribeMode)
        {
            await RunTranscriptionModeAsync();
            return;
        }

        // Configuration
        var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        var usePlugins = Environment.GetEnvironmentVariable("USE_PLUGINS")?.ToLower() == "true";
        var pluginsPath = Environment.GetEnvironmentVariable("PLUGINS_PATH");
        var databasePath = Environment.GetEnvironmentVariable("DATABASE_PATH") ?? "./bot.db";

        // Indexing configuration
        var enableIndexing = Environment.GetEnvironmentVariable("ENABLE_INDEXING")?.ToLower() == "true";
        var storagePath = Environment.GetEnvironmentVariable("STORAGE_PATH") ?? "./storage";
        var tempPath = Environment.GetEnvironmentVariable("TEMP_PATH") ?? "./temp";

        // TaskAI configuration
        var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var taskAiModel = Environment.GetEnvironmentVariable("TASKAI_MODEL") ?? "gpt-4o-mini";

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
            Console.WriteLine("  # Optional: Enable indexing for search");
            Console.WriteLine("  export ENABLE_INDEXING=true");
            Console.WriteLine("  export STORAGE_PATH=./storage               # optional");
            Console.WriteLine("  export TEMP_PATH=./temp                     # optional");
            Console.WriteLine();
            Console.WriteLine("  # Optional: Enable TaskAI for task management");
            Console.WriteLine("  export OPENAI_API_KEY=your_openai_api_key");
            Console.WriteLine("  export TASKAI_MODEL=gpt-4o-mini             # optional");
            Console.WriteLine();
            Console.WriteLine("  dotnet run");
            Environment.Exit(1);
            return;
        }

        Console.WriteLine("=== Forward Message Analyzer Bot ===");

        // Initialize database
        var db = new BotDb(databasePath);
        await db.InitializeAsync();
        var dbExists = File.Exists(databasePath);
        Console.WriteLine($"Database: {databasePath} {(dbExists ? "(loaded)" : "(created)")}");
        Console.WriteLine();

        BotService botService;

        if (usePlugins)
        {
            Console.WriteLine("Mode: C# Plugin System");
            Console.WriteLine();

            var analyzerService = new AnalyzerService(pluginsPath);
            analyzerService.Initialize(enableHotReload: true);
            Console.WriteLine();

            // Create TaskAI service if OpenAI key is configured
            ITaskAiService taskAiService = null;
            if (!string.IsNullOrEmpty(openAiApiKey))
            {
                Console.WriteLine("TaskAI: Enabled");
                Console.WriteLine($"  Model: {taskAiModel}");
                Console.WriteLine();

                var aiConfig = new AiServiceConfig
                {
                    Enabled = true,
                    OpenAiApiKey = openAiApiKey,
                    OpenAiModel = taskAiModel
                };
                var aiService = new OpenAiService(aiConfig);

                var taskAiConfig = new TaskAiConfig { Model = taskAiModel };
                taskAiService = new TaskAiService(aiService, taskAiConfig);
            }

            if (enableIndexing)
            {
                Console.WriteLine("Indexing: Enabled");
                Console.WriteLine($"  Storage: {storagePath}");
                Console.WriteLine($"  Temp: {tempPath}");
                Console.WriteLine();

                // Create indexing config with in-memory service (for now)
                // TODO: Replace with KernelMemoryService for production
                var memoryService = new InMemoryService();
                var indexingConfig = new IndexingConfig
                {
                    TempPath = tempPath,
                    StoragePath = storagePath,
                    MemoryService = memoryService
                };

                botService = new BotService(
                    token: token,
                    db: db,
                    analyzerService: analyzerService,
                    indexingConfig: indexingConfig,
                    concurrency: 1,
                    taskAiService: taskAiService
                );
            }
            else
            {
                botService = new BotService(
                    token: token,
                    db: db,
                    analyzerService: analyzerService,
                    indexingConfig: null,
                    concurrency: 1,
                    taskAiService: taskAiService
                );
            }
        }
        else
        {
            Console.WriteLine("Mode: Legacy Shell Scripts");
            Console.WriteLine($"Text script:  {textScriptPath}");
            Console.WriteLine($"Media script: {mediaScriptPath}");
            Console.WriteLine();

            botService = new BotService(
                token: token,
                db: db,
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

    static async Task RunTranscriptionModeAsync()
    {
        var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        var outputPath = Environment.GetEnvironmentVariable("TRANSCRIBE_OUTPUT") ?? $"./transcripts_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var tempPath = Environment.GetEnvironmentVariable("TEMP_PATH") ?? "./temp";
        var whisperCliPath = Environment.GetEnvironmentVariable("WHISPER_CLI_PATH");
        var whisperModelPath = Environment.GetEnvironmentVariable("WHISPER_MODEL_PATH");

        if (string.IsNullOrEmpty(token))
        {
            Console.WriteLine("Error: TELEGRAM_BOT_TOKEN environment variable is not set.");
            Environment.Exit(1);
            return;
        }

        Console.WriteLine("=== Transcription Bot ===");
        Console.WriteLine($"Output: {outputPath}");
        Console.WriteLine();

        var botService = new TranscriptionBotService(
            token,
            outputPath,
            tempPath,
            whisperCliPath,
            whisperModelPath
        );

        using (botService)
        {
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

            await botService.StartAsync();

            Console.WriteLine("Forward messages to me. Press Ctrl+C to stop.");
            Console.WriteLine();

            shutdownEvent.Wait();

            await botService.StopAsync();

            Console.WriteLine("Bot stopped. Goodbye!");
        }
    }
}
