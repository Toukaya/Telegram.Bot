namespace BotDatabase.Entities;

public class ReportWeek
{
    public int Id { get; set; }
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public int WeekNumber { get; set; }
    public int Year { get; set; }

    // Summary stats
    public int TotalActiveMembers { get; set; }
    public int SubmittedCount { get; set; }
    public int MissingCount { get; set; }
    public int OnVacationCount { get; set; }

    // Generated files
    public string ReportFilePath { get; set; } = "";
    public string TrackerChPath { get; set; } = "";
    public string TrackerEnPath { get; set; } = "";

    public DateTime GeneratedAt { get; set; }
    public DateTime SentAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
