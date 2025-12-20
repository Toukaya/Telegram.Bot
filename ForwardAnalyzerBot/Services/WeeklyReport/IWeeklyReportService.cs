using BotDatabase.Entities;

namespace ForwardAnalyzerBot.Services.WeeklyReport;

public interface IWeeklyReportService
{
    // Submission
    Task<BotDatabase.Entities.WeeklyReport> SubmitReportAsync(WeeklyReportSubmission submission, DateTime weekStart = default);
    Task<BotDatabase.Entities.WeeklyReport> SubmitReportByMemberIdAsync(int memberId, string doneThisWeek, string plannedNextWeek, string blockers, DateTime weekStart = default);

    // Queries
    Task<BotDatabase.Entities.WeeklyReport> GetReportAsync(int memberId, DateTime weekStart);
    Task<List<BotDatabase.Entities.WeeklyReport>> GetWeekReportsAsync(DateTime weekStart = default);
    Task<List<BotDatabase.Entities.WeeklyReport>> GetMemberHistoryAsync(int memberId, int weeks = 4);

    // Week status
    Task<WeekStatus> GetWeekStatusAsync(DateTime weekStart = default);
    Task<List<TeamMember>> GetMissingMembersAsync(DateTime weekStart = default);
    Task<List<TeamMember>> GetSubmittedMembersAsync(DateTime weekStart = default);

    // Analysis
    Task<string> CalculateRiskLevelAsync(int memberId, DateTime weekStart = default);
    Task<int> CalculateDurationWeeksAsync(int memberId, DateTime weekStart = default);

    // Utilities
    DateTime GetCurrentWeekStart();
    DateTime GetWeekStart(DateTime date);
    (DateTime start, DateTime end) GetWeekRange(DateTime date);
    int GetWeekNumber(DateTime date);
}
