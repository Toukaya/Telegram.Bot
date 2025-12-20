using System.Globalization;
using BotDatabase.Entities;
using BotDatabase.Services;

namespace ForwardAnalyzerBot.Services.WeeklyReport;

public class WeeklyReportService : IWeeklyReportService
{
    private readonly BotDb _db;

    public WeeklyReportService(BotDb db)
    {
        _db = db;
    }

    // ========== Submission ==========

    public async Task<BotDatabase.Entities.WeeklyReport> SubmitReportAsync(WeeklyReportSubmission submission, DateTime weekStart = default)
    {
        if (weekStart == default)
        {
            weekStart = GetCurrentWeekStart();
        }

        var member = await _db.TeamMembers.FindByAlias(submission.Alias);
        if (member == null)
        {
            member = await _db.TeamMembers.MatchByName(submission.Alias);
        }

        if (member == null)
        {
            throw new ArgumentException($"Team member not found: {submission.Alias}");
        }

        var (start, end) = GetWeekRange(weekStart);

        var report = new BotDatabase.Entities.WeeklyReport
        {
            TeamMemberId = member.Id,
            WeekStart = start,
            WeekEnd = end,
            DoneThisWeek = submission.DoneThisWeek,
            PlannedNextWeek = submission.PlannedNextWeek,
            Blockers = submission.Blockers,
            RawContent = submission.RawContent,
            SubmittedVia = submission.SubmittedVia,
            DurationWeeks = await CalculateDurationWeeksAsync(member.Id, start)
        };

        return await _db.WeeklyReports.Upsert(report);
    }

    public async Task<BotDatabase.Entities.WeeklyReport> SubmitReportByMemberIdAsync(
        int memberId, string doneThisWeek, string plannedNextWeek, string blockers, DateTime weekStart = default)
    {
        if (weekStart == default)
        {
            weekStart = GetCurrentWeekStart();
        }

        var (start, end) = GetWeekRange(weekStart);

        var report = new BotDatabase.Entities.WeeklyReport
        {
            TeamMemberId = memberId,
            WeekStart = start,
            WeekEnd = end,
            DoneThisWeek = doneThisWeek,
            PlannedNextWeek = plannedNextWeek,
            Blockers = blockers,
            SubmittedVia = SubmissionMethod.Manual,
            DurationWeeks = await CalculateDurationWeeksAsync(memberId, start)
        };

        return await _db.WeeklyReports.Upsert(report);
    }

    // ========== Queries ==========

    public async Task<BotDatabase.Entities.WeeklyReport> GetReportAsync(int memberId, DateTime weekStart)
    {
        return await _db.WeeklyReports.FindByMemberAndWeek(memberId, GetWeekStart(weekStart));
    }

    public async Task<List<BotDatabase.Entities.WeeklyReport>> GetWeekReportsAsync(DateTime weekStart = default)
    {
        if (weekStart == default)
        {
            weekStart = GetCurrentWeekStart();
        }
        return await _db.WeeklyReports.GetByWeekAsync(GetWeekStart(weekStart));
    }

    public async Task<List<BotDatabase.Entities.WeeklyReport>> GetMemberHistoryAsync(int memberId, int weeks = 4)
    {
        return await _db.WeeklyReports.GetByMemberAsync(memberId, weeks);
    }

    // ========== Week Status ==========

    public async Task<WeekStatus> GetWeekStatusAsync(DateTime weekStart = default)
    {
        if (weekStart == default)
        {
            weekStart = GetCurrentWeekStart();
        }

        var (start, end) = GetWeekRange(weekStart);

        // Get all active members (not left)
        var allMembers = await _db.TeamMembers.GetAllAsync();
        var activeMembers = allMembers.Where(m => m.Status == TeamMemberStatus.Active).ToList();
        var vacationMembers = allMembers.Where(m => m.Status == TeamMemberStatus.Vacation).ToList();

        // Get submitted member IDs
        var submittedIds = await _db.WeeklyReports.GetSubmittedMemberIdsAsync(start);
        var submittedMembers = activeMembers.Where(m => submittedIds.Contains(m.Id)).ToList();
        var missingMembers = activeMembers.Where(m => !submittedIds.Contains(m.Id)).ToList();

        return new WeekStatus
        {
            WeekStart = start,
            WeekEnd = end,
            WeekNumber = GetWeekNumber(start),
            Year = start.Year,
            TotalActive = activeMembers.Count,
            SubmittedCount = submittedMembers.Count,
            MissingCount = missingMembers.Count,
            OnVacationCount = vacationMembers.Count,
            SubmittedMembers = submittedMembers,
            MissingMembers = missingMembers,
            VacationMembers = vacationMembers
        };
    }

    public async Task<List<TeamMember>> GetMissingMembersAsync(DateTime weekStart = default)
    {
        var status = await GetWeekStatusAsync(weekStart);
        return status.MissingMembers;
    }

    public async Task<List<TeamMember>> GetSubmittedMembersAsync(DateTime weekStart = default)
    {
        var status = await GetWeekStatusAsync(weekStart);
        return status.SubmittedMembers;
    }

    // ========== Analysis ==========

    public async Task<string> CalculateRiskLevelAsync(int memberId, DateTime weekStart = default)
    {
        if (weekStart == default)
        {
            weekStart = GetCurrentWeekStart();
        }
        weekStart = GetWeekStart(weekStart);
        var report = await GetReportAsync(memberId, weekStart);

        // No report this week = high risk
        if (report == null)
        {
            // Check if also missing last week
            var lastWeek = weekStart.AddDays(-7);
            var lastReport = await GetReportAsync(memberId, lastWeek);
            if (lastReport == null)
            {
                return ReportRiskLevel.High; // 2+ consecutive missing
            }
            return ReportRiskLevel.Medium; // 1 week missing
        }

        // Has blockers = medium risk
        if (!string.IsNullOrWhiteSpace(report.Blockers))
        {
            return ReportRiskLevel.Medium;
        }

        // Duration >= 3 weeks = high risk
        if (report.DurationWeeks >= 3)
        {
            return ReportRiskLevel.High;
        }

        // Duration == 2 weeks = medium risk
        if (report.DurationWeeks == 2)
        {
            return ReportRiskLevel.Medium;
        }

        return ReportRiskLevel.Low;
    }

    public async Task<int> CalculateDurationWeeksAsync(int memberId, DateTime weekStart = default)
    {
        if (weekStart == default)
        {
            weekStart = GetCurrentWeekStart();
        }

        // Get recent reports for this member
        var history = await _db.WeeklyReports.GetByMemberAsync(memberId, 10);
        if (history.Count == 0)
        {
            return 1; // First report
        }

        // Simple heuristic: count consecutive weeks with reports
        // More sophisticated: compare task similarity (future enhancement)
        int duration = 1;
        var currentWeek = GetWeekStart(weekStart);

        foreach (var report in history.OrderByDescending(r => r.WeekStart))
        {
            var reportWeek = GetWeekStart(report.WeekStart);
            var expectedPrevWeek = currentWeek.AddDays(-7);

            if (reportWeek == expectedPrevWeek)
            {
                duration++;
                currentWeek = reportWeek;
            }
            else if (reportWeek < expectedPrevWeek)
            {
                break; // Gap in reports
            }
        }

        return duration;
    }

    // ========== Utilities ==========

    public DateTime GetCurrentWeekStart()
    {
        return GetWeekStart(DateTime.UtcNow);
    }

    public DateTime GetWeekStart(DateTime date)
    {
        // Get Monday of the week
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }

    public (DateTime start, DateTime end) GetWeekRange(DateTime date)
    {
        var start = GetWeekStart(date);
        var end = start.AddDays(4); // Friday
        return (start, end);
    }

    public int GetWeekNumber(DateTime date)
    {
        var calendar = CultureInfo.InvariantCulture.Calendar;
        return calendar.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }
}
