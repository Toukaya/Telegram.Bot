namespace BotDatabase.Entities;

public class WeeklyReport
{
    public int Id { get; set; }
    public int TeamMemberId { get; set; }
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }

    // Content
    public string DoneThisWeek { get; set; } = "";
    public string PlannedNextWeek { get; set; } = "";
    public string Blockers { get; set; } = "";
    public string RawContent { get; set; } = "";

    // Submission metadata
    public DateTime SubmittedAt { get; set; }
    public string SubmittedVia { get; set; } = SubmissionMethod.Manual;
    public long MessageId { get; set; }

    // Analysis
    public string ParsedTasks { get; set; } = "";
    public string RiskLevel { get; set; } = ReportRiskLevel.Low;
    public int DurationWeeks { get; set; } = 1;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public TeamMember TeamMember { get; set; }
}

public static class SubmissionMethod
{
    public const string Manual = "manual";
    public const string Command = "command";
    public const string Forward = "forward";
    public const string Text = "text";
}

public static class ReportRiskLevel
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
}
