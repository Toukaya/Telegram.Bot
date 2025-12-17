namespace ForwardAnalyzerBot.Services;

public static class Logger
{
    private static readonly object _lock = new();

    public enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error
    }

    public static LogLevel MinLevel { get; set; } = LogLevel.Info;

    public static void Debug(string tag, string message) => Log(LogLevel.Debug, tag, message);
    public static void Info(string tag, string message) => Log(LogLevel.Info, tag, message);
    public static void Warn(string tag, string message) => Log(LogLevel.Warn, tag, message);
    public static void Error(string tag, string message) => Log(LogLevel.Error, tag, message);
    public static void Error(string tag, string message, Exception ex) => Log(LogLevel.Error, tag, $"{message}: {ex.Message}");

    private static void Log(LogLevel level, string tag, string message)
    {
        if (level < MinLevel) return;

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var levelStr = level switch
        {
            LogLevel.Debug => "DBG",
            LogLevel.Info => "INF",
            LogLevel.Warn => "WRN",
            LogLevel.Error => "ERR",
            _ => "???"
        };

        var color = level switch
        {
            LogLevel.Debug => ConsoleColor.Gray,
            LogLevel.Info => ConsoleColor.White,
            LogLevel.Warn => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            _ => ConsoleColor.White
        };

        lock (_lock)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{timestamp}] ");
            Console.ForegroundColor = color;
            Console.Write($"[{levelStr}] ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"[{tag}] ");
            Console.ForegroundColor = originalColor;
            Console.WriteLine(message);
        }
    }
}
