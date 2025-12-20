namespace ForwardAnalyzerBot.Services.WeeklyReport;

public interface IReportGeneratorService
{
    // Generate individual files
    Task<string> GenerateWeeklyReportTxtAsync(DateTime weekStart = default);
    Task<string> GenerateStatusTrackerChAsync(DateTime weekStart = default);
    Task<string> GenerateStatusTrackerEnAsync(DateTime weekStart = default);

    // Generate all files
    Task<GeneratedReportPaths> GenerateAllReportsAsync(DateTime weekStart = default);

    // Append to existing tracker files
    Task AppendToTrackerChAsync(DateTime weekStart, string existingFilePath);
    Task AppendToTrackerEnAsync(DateTime weekStart, string existingFilePath);

    // Configuration
    void SetOutputDirectory(string path);
    void SetTrackerPaths(string chPath, string enPath);
}
