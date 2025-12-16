namespace BotDatabase.Entities;

public class AnalysisResult
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public string ScriptType { get; set; } = "";   // text, media
    public string Status { get; set; } = "";       // pending, running, completed, failed
    public string Result { get; set; } = "";       // JSON output from script
    public string Error { get; set; } = "";
    public int ExitCode { get; set; }
    public long ExecutionTimeMs { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime CompletedAt { get; set; }

    // Navigation
    public Message Message { get; set; }
}

public static class AnalysisStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

public static class ScriptTypes
{
    public const string Text = "text";
    public const string Media = "media";
}
