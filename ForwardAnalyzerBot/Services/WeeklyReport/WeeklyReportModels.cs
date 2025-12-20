using BotDatabase.Entities;

namespace ForwardAnalyzerBot.Services.WeeklyReport;

public class WeekStatus
{
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public int WeekNumber { get; set; }
    public int Year { get; set; }

    public int TotalActive { get; set; }
    public int SubmittedCount { get; set; }
    public int MissingCount { get; set; }
    public int OnVacationCount { get; set; }

    public List<TeamMember> SubmittedMembers { get; set; } = new();
    public List<TeamMember> MissingMembers { get; set; } = new();
    public List<TeamMember> VacationMembers { get; set; } = new();
}

public class WeeklyReportSubmission
{
    public string Alias { get; set; } = "";
    public string DoneThisWeek { get; set; } = "";
    public string PlannedNextWeek { get; set; } = "";
    public string Blockers { get; set; } = "";
    public string RawContent { get; set; } = "";
    public string SubmittedVia { get; set; } = SubmissionMethod.Manual;
}

public class GeneratedReportPaths
{
    public string WeeklyReportTxt { get; set; } = "";
    public string StatusTrackerCh { get; set; } = "";
    public string StatusTrackerEn { get; set; } = "";
}

public class WeeklyReportConfig
{
    public string OutputDirectory { get; set; } = "./reports";
    public string TrackerChPath { get; set; } = "";
    public string TrackerEnPath { get; set; } = "";
    public long AdminChatId { get; set; }
}
