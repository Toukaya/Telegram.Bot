using System.Text;
using BotDatabase.Entities;
using BotDatabase.Services;

namespace ForwardAnalyzerBot.Services.WeeklyReport;

public class ReportGeneratorService : IReportGeneratorService
{
    private readonly BotDb _db;
    private readonly IWeeklyReportService _reportService;
    private string _outputDirectory = "./reports";
    private string _trackerChPath = "";
    private string _trackerEnPath = "";

    public ReportGeneratorService(BotDb db, IWeeklyReportService reportService)
    {
        _db = db;
        _reportService = reportService;
    }

    public void SetOutputDirectory(string path)
    {
        _outputDirectory = path;
        if (!Directory.Exists(_outputDirectory))
        {
            Directory.CreateDirectory(_outputDirectory);
        }
    }

    public void SetTrackerPaths(string chPath, string enPath)
    {
        _trackerChPath = chPath;
        _trackerEnPath = enPath;
    }

    // ========== Generate Weekly Report TXT ==========

    public async Task<string> GenerateWeeklyReportTxtAsync(DateTime weekStart = default)
    {
        var status = await _reportService.GetWeekStatusAsync(weekStart);
        var reports = await _reportService.GetWeekReportsAsync(weekStart);

        var sb = new StringBuilder();

        // Header
        sb.AppendLine("KCG Game Team Weekly Report");
        sb.AppendLine($"Week: {status.WeekStart:MMMM d}–{status.WeekEnd:d}, {status.Year} (W{status.WeekNumber})");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine();

        // Summary
        sb.AppendLine($"SUBMITTED REPORTS: {status.SubmittedCount} of {status.TotalActive} active developers");
        if (status.OnVacationCount > 0)
        {
            var vacationNames = string.Join(", ", status.VacationMembers.Select(m => m.Alias));
            sb.AppendLine($"ON VACATION: {vacationNames}");
        }
        if (status.MissingCount > 0)
        {
            var missingNames = string.Join(", ", status.MissingMembers.Select(m => m.Alias));
            sb.AppendLine($"MISSING REPORTS: {missingNames}");
        }
        sb.AppendLine();

        // Developer reports
        sb.AppendLine(new string('=', 50));
        sb.AppendLine("DEVELOPER REPORTS");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine();

        var developerReports = reports.Where(r => r.TeamMember.Role == TeamMemberRole.Developer).ToList();
        foreach (var report in developerReports)
        {
            sb.AppendLine(new string('-', 50));
            sb.AppendLine(report.TeamMember.Name);
            sb.AppendLine(new string('-', 50));
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(report.DoneThisWeek))
            {
                sb.AppendLine("What I've done this week:");
                sb.AppendLine();
                sb.AppendLine(report.DoneThisWeek.Trim());
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(report.PlannedNextWeek))
            {
                sb.AppendLine("What I will be doing next:");
                sb.AppendLine();
                sb.AppendLine(report.PlannedNextWeek.Trim());
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(report.Blockers))
            {
                sb.AppendLine("Current blockers:");
                sb.AppendLine();
                sb.AppendLine(report.Blockers.Trim());
                sb.AppendLine();
            }
        }

        // Non-developer reports (designers)
        var designerReports = reports.Where(r => r.TeamMember.Role == TeamMemberRole.Designer).ToList();
        if (designerReports.Any())
        {
            sb.AppendLine(new string('=', 50));
            sb.AppendLine("NON-DEVELOPER REPORTS (Art/Design)");
            sb.AppendLine(new string('=', 50));
            sb.AppendLine();

            foreach (var report in designerReports)
            {
                sb.AppendLine(new string('-', 50));
                sb.AppendLine(report.TeamMember.Name);
                sb.AppendLine(new string('-', 50));
                sb.AppendLine();

                if (!string.IsNullOrWhiteSpace(report.DoneThisWeek))
                {
                    sb.AppendLine("What I've done this week:");
                    sb.AppendLine();
                    sb.AppendLine(report.DoneThisWeek.Trim());
                    sb.AppendLine();
                }
            }
        }

        // Team status
        sb.AppendLine(new string('=', 50));
        sb.AppendLine("TEAM STATUS");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine();

        if (status.VacationMembers.Any())
        {
            sb.AppendLine("On Vacation:");
            foreach (var member in status.VacationMembers)
            {
                sb.AppendLine($"- {member.Name}");
            }
            sb.AppendLine();
        }

        // Get left members
        var leftMembers = await _db.TeamMembers.GetByStatusAsync(TeamMemberStatus.Left);
        if (leftMembers.Any())
        {
            sb.AppendLine("Team Members Who Left:");
            foreach (var member in leftMembers.OrderByDescending(m => m.LeftAt))
            {
                var leftWeek = _reportService.GetWeekNumber(member.LeftAt);
                sb.AppendLine($"- {member.Name} (left W{leftWeek})");
            }
            sb.AppendLine();
        }

        sb.AppendLine(new string('=', 50));
        sb.AppendLine("END OF REPORT");
        sb.AppendLine(new string('=', 50));

        // Save file
        var fileName = $"{status.WeekStart:yyyyMMdd}-{status.WeekEnd:yyyyMMdd} - kcg - game - weekly report.txt";
        var filePath = Path.Combine(_outputDirectory, fileName);

        if (!Directory.Exists(_outputDirectory))
        {
            Directory.CreateDirectory(_outputDirectory);
        }

        await File.WriteAllTextAsync(filePath, sb.ToString());
        return filePath;
    }

    // ========== Generate Status Tracker (Chinese) ==========

    public async Task<string> GenerateStatusTrackerChAsync(DateTime weekStart = default)
    {
        var status = await _reportService.GetWeekStatusAsync(weekStart);
        var reports = await _reportService.GetWeekReportsAsync(weekStart);

        var sb = new StringBuilder();

        sb.AppendLine($"# {status.Year}-W{status.WeekNumber} ({status.WeekStart:MMM d}–{status.WeekEnd:d}, {status.Year})");
        sb.AppendLine();
        sb.AppendLine("## 开发人员状态表");
        sb.AppendLine();
        sb.AppendLine("| 成员 | 本周核心任务 | 持续周期 | 协同/重叠警报 | 风险等级 | 当前阻塞点 | 管理建议 |");
        sb.AppendLine("|:---|:---|:---:|:---:|:---:|:---|:---|");

        // Developer rows
        var developers = await _db.TeamMembers.GetDevelopersAsync();
        foreach (var member in developers.Where(m => m.Status != TeamMemberStatus.Left))
        {
            var report = reports.FirstOrDefault(r => r.TeamMemberId == member.Id);
            var riskLevel = await _reportService.CalculateRiskLevelAsync(member.Id, status.WeekStart);

            if (member.Status == TeamMemberStatus.Vacation)
            {
                sb.AppendLine($"| {member.Alias} | - | - | - | - | 休假中 | 休假 |");
            }
            else if (report != null)
            {
                var tasks = FormatTasksForTable(report.DoneThisWeek);
                var blocker = string.IsNullOrWhiteSpace(report.Blockers) ? "无" : EscapeMarkdownTable(report.Blockers.Split('\n')[0].Trim());
                var riskCh = riskLevel == ReportRiskLevel.High ? "高" : riskLevel == ReportRiskLevel.Medium ? "中" : "低";
                sb.AppendLine($"| {member.Alias} | {tasks} | 第 {report.DurationWeeks} 周 | 正常协同 | {riskCh} | {blocker} | - |");
            }
            else
            {
                sb.AppendLine($"| {member.Alias} | - | - | - | 高 | 未提交周报 | 请提交周报 |");
            }
        }

        sb.AppendLine();

        // Designer section
        var designers = await _db.TeamMembers.GetDesignersAsync();
        if (designers.Any())
        {
            sb.AppendLine("## 非开发人员状态 (Art/Design)");
            sb.AppendLine();
            sb.AppendLine("| 成员 | 本周核心任务 | 协同 | 备注 |");
            sb.AppendLine("|:---|:---|:---|:---|");

            foreach (var member in designers.Where(m => m.Status != TeamMemberStatus.Left))
            {
                var report = reports.FirstOrDefault(r => r.TeamMemberId == member.Id);
                if (report != null)
                {
                    var tasks = FormatTasksForTable(report.DoneThisWeek);
                    sb.AppendLine($"| {member.Alias} | {tasks} | - | 已提交周报 |");
                }
                else
                {
                    sb.AppendLine($"| {member.Alias} | - | - | 未提交周报 |");
                }
            }
            sb.AppendLine();
        }

        // Risk summary
        sb.AppendLine("## 风险汇总 (仅开发人员)");
        sb.AppendLine();
        sb.AppendLine("| 风险等级 | 成员数 | 成员 |");
        sb.AppendLine("|:---:|:---:|:---|");

        var highRisk = new List<string>();
        var mediumRisk = new List<string>();
        var lowRisk = new List<string>();

        foreach (var member in developers.Where(m => m.Status == TeamMemberStatus.Active))
        {
            var risk = await _reportService.CalculateRiskLevelAsync(member.Id, status.WeekStart);
            if (risk == ReportRiskLevel.High) highRisk.Add(member.Alias);
            else if (risk == ReportRiskLevel.Medium) mediumRisk.Add(member.Alias);
            else lowRisk.Add(member.Alias);
        }

        sb.AppendLine($"| 高 | {highRisk.Count} | {(highRisk.Any() ? string.Join(", ", highRisk) : "-")} |");
        sb.AppendLine($"| 中 | {mediumRisk.Count} | {(mediumRisk.Any() ? string.Join(", ", mediumRisk) : "-")} |");
        sb.AppendLine($"| 低 | {lowRisk.Count} | {(lowRisk.Any() ? string.Join(", ", lowRisk) : "-")} |");
        sb.AppendLine();

        // Highlights
        sb.AppendLine("## 本周进展亮点");
        sb.AppendLine();

        foreach (var report in reports.Where(r => r.TeamMember.Role == TeamMemberRole.Developer))
        {
            var highlight = ExtractHighlight(report.DoneThisWeek);
            if (!string.IsNullOrWhiteSpace(highlight))
            {
                sb.AppendLine($"- **{report.TeamMember.Alias}**: {highlight}");
            }
        }

        foreach (var report in reports.Where(r => r.TeamMember.Role == TeamMemberRole.Designer))
        {
            var highlight = ExtractHighlight(report.DoneThisWeek);
            if (!string.IsNullOrWhiteSpace(highlight))
            {
                sb.AppendLine($"- **{report.TeamMember.Alias}** (Art/Design): {highlight}");
            }
        }

        return sb.ToString();
    }

    // ========== Generate Status Tracker (English) ==========

    public async Task<string> GenerateStatusTrackerEnAsync(DateTime weekStart = default)
    {
        var status = await _reportService.GetWeekStatusAsync(weekStart);
        var reports = await _reportService.GetWeekReportsAsync(weekStart);

        var sb = new StringBuilder();

        sb.AppendLine($"# {status.Year}-W{status.WeekNumber} ({status.WeekStart:MMM d}–{status.WeekEnd:d}, {status.Year})");
        sb.AppendLine();
        sb.AppendLine("## Developer Status Table");
        sb.AppendLine();
        sb.AppendLine("| Owner | Key Tasks This Week | Duration | Overlap Alert | Risk Level | Current Blocker | Manager Notes |");
        sb.AppendLine("|:---|:---|:---:|:---:|:---:|:---|:---|");

        // Developer rows
        var developersEn = await _db.TeamMembers.GetDevelopersAsync();
        foreach (var member in developersEn.Where(m => m.Status != TeamMemberStatus.Left))
        {
            var report = reports.FirstOrDefault(r => r.TeamMemberId == member.Id);
            var riskLevel = await _reportService.CalculateRiskLevelAsync(member.Id, status.WeekStart);

            if (member.Status == TeamMemberStatus.Vacation)
            {
                sb.AppendLine($"| {member.Alias} | - | - | - | - | On vacation | On vacation |");
            }
            else if (report != null)
            {
                var tasks = FormatTasksForTable(report.DoneThisWeek);
                var blocker = string.IsNullOrWhiteSpace(report.Blockers) ? "None" : EscapeMarkdownTable(report.Blockers.Split('\n')[0].Trim());
                var riskEn = riskLevel == ReportRiskLevel.High ? "High" : riskLevel == ReportRiskLevel.Medium ? "Medium" : "Low";
                sb.AppendLine($"| {member.Alias} | {tasks} | Week {report.DurationWeeks} | Normal | {riskEn} | {blocker} | - |");
            }
            else
            {
                sb.AppendLine($"| {member.Alias} | - | - | - | High | Missing report | Please submit report |");
            }
        }

        sb.AppendLine();

        // Designer section
        var designersEn = await _db.TeamMembers.GetDesignersAsync();
        if (designersEn.Any())
        {
            sb.AppendLine("## Non-Developer Status (Art/Design)");
            sb.AppendLine();
            sb.AppendLine("| Owner | Key Tasks This Week | Collaboration | Notes |");
            sb.AppendLine("|:---|:---|:---|:---|");

            foreach (var member in designersEn.Where(m => m.Status != TeamMemberStatus.Left))
            {
                var report = reports.FirstOrDefault(r => r.TeamMemberId == member.Id);
                if (report != null)
                {
                    var tasks = FormatTasksForTable(report.DoneThisWeek);
                    sb.AppendLine($"| {member.Alias} | {tasks} | - | Report submitted |");
                }
                else
                {
                    sb.AppendLine($"| {member.Alias} | - | - | Missing report |");
                }
            }
            sb.AppendLine();
        }

        // Risk summary
        sb.AppendLine("## Risk Summary (Developers Only)");
        sb.AppendLine();
        sb.AppendLine("| Risk Level | Count | Members |");
        sb.AppendLine("|:---:|:---:|:---|");

        var highRisk = new List<string>();
        var mediumRisk = new List<string>();
        var lowRisk = new List<string>();

        foreach (var member in developersEn.Where(m => m.Status == TeamMemberStatus.Active))
        {
            var risk = await _reportService.CalculateRiskLevelAsync(member.Id, status.WeekStart);
            if (risk == ReportRiskLevel.High) highRisk.Add(member.Alias);
            else if (risk == ReportRiskLevel.Medium) mediumRisk.Add(member.Alias);
            else lowRisk.Add(member.Alias);
        }

        sb.AppendLine($"| High | {highRisk.Count} | {(highRisk.Any() ? string.Join(", ", highRisk) : "-")} |");
        sb.AppendLine($"| Medium | {mediumRisk.Count} | {(mediumRisk.Any() ? string.Join(", ", mediumRisk) : "-")} |");
        sb.AppendLine($"| Low | {lowRisk.Count} | {(lowRisk.Any() ? string.Join(", ", lowRisk) : "-")} |");
        sb.AppendLine();

        // Highlights
        sb.AppendLine("## This Week's Highlights");
        sb.AppendLine();

        foreach (var report in reports.Where(r => r.TeamMember.Role == TeamMemberRole.Developer))
        {
            var highlight = ExtractHighlight(report.DoneThisWeek);
            if (!string.IsNullOrWhiteSpace(highlight))
            {
                sb.AppendLine($"- **{report.TeamMember.Alias}**: {highlight}");
            }
        }

        foreach (var report in reports.Where(r => r.TeamMember.Role == TeamMemberRole.Designer))
        {
            var highlight = ExtractHighlight(report.DoneThisWeek);
            if (!string.IsNullOrWhiteSpace(highlight))
            {
                sb.AppendLine($"- **{report.TeamMember.Alias}** (Art/Design): {highlight}");
            }
        }

        return sb.ToString();
    }

    // ========== Generate All Reports ==========

    public async Task<GeneratedReportPaths> GenerateAllReportsAsync(DateTime weekStart = default)
    {
        var txtPath = await GenerateWeeklyReportTxtAsync(weekStart);

        var chContent = await GenerateStatusTrackerChAsync(weekStart);
        var enContent = await GenerateStatusTrackerEnAsync(weekStart);

        var status = await _reportService.GetWeekStatusAsync(weekStart);

        string chPath, enPath;

        if (!string.IsNullOrEmpty(_trackerChPath) && File.Exists(_trackerChPath))
        {
            // Append to existing file
            await AppendToTrackerChAsync(weekStart, _trackerChPath);
            chPath = _trackerChPath;
        }
        else
        {
            // Create new file
            var chFileName = $"member-status-tracker-ch-W{status.WeekNumber}.md";
            chPath = Path.Combine(_outputDirectory, chFileName);
            await File.WriteAllTextAsync(chPath, chContent);
        }

        if (!string.IsNullOrEmpty(_trackerEnPath) && File.Exists(_trackerEnPath))
        {
            // Append to existing file
            await AppendToTrackerEnAsync(weekStart, _trackerEnPath);
            enPath = _trackerEnPath;
        }
        else
        {
            // Create new file
            var enFileName = $"member-status-tracker-en-W{status.WeekNumber}.md";
            enPath = Path.Combine(_outputDirectory, enFileName);
            await File.WriteAllTextAsync(enPath, enContent);
        }

        return new GeneratedReportPaths
        {
            WeeklyReportTxt = txtPath,
            StatusTrackerCh = chPath,
            StatusTrackerEn = enPath
        };
    }

    // ========== Append to Existing Trackers ==========

    public async Task AppendToTrackerChAsync(DateTime weekStart, string existingFilePath)
    {
        var newContent = await GenerateStatusTrackerChAsync(weekStart);
        var existing = await File.ReadAllTextAsync(existingFilePath);

        var combined = existing.TrimEnd() + "\n\n---\n\n" + newContent;
        await File.WriteAllTextAsync(existingFilePath, combined);
    }

    public async Task AppendToTrackerEnAsync(DateTime weekStart, string existingFilePath)
    {
        var newContent = await GenerateStatusTrackerEnAsync(weekStart);
        var existing = await File.ReadAllTextAsync(existingFilePath);

        var combined = existing.TrimEnd() + "\n\n---\n\n" + newContent;
        await File.WriteAllTextAsync(existingFilePath, combined);
    }

    // ========== Helper Methods ==========

    private string FormatTasksForTable(string tasks)
    {
        if (string.IsNullOrWhiteSpace(tasks))
            return "-";

        var lines = tasks.Split('\n')
            .Select(l => l.Trim().TrimStart('-', '*', '•').Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Take(3)
            .ToList();

        if (lines.Count == 0)
            return "-";

        // Format as numbered list with <br>, escape pipe characters
        var numbered = lines.Select((l, i) => $"{i + 1}. {EscapeMarkdownTable(TruncateText(l, 50))}");
        return string.Join("<br>", numbered);
    }

    private string ExtractHighlight(string tasks)
    {
        if (string.IsNullOrWhiteSpace(tasks))
            return "";

        var lines = tasks.Split('\n')
            .Select(l => l.Trim().TrimStart('-', '*', '•').Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count == 0)
            return "";

        // Return first task as highlight
        return TruncateText(lines[0], 80);
    }

    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        text = text.Replace("\n", " ").Replace("\r", "").Trim();

        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength - 3) + "...";
    }

    private string EscapeMarkdownTable(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        // Escape pipe characters that would break markdown tables
        return text.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "").Trim();
    }
}
