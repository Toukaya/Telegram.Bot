using Telegram.Bot;

namespace TelegramBotService.Services;

// Result of downloading a file from Telegram
public class DownloadResult
{
    public bool Success { get; set; }
    public string LocalPath { get; set; } = "";
    public long FileSize { get; set; }
    public string Error { get; set; } = "";

    public static DownloadResult Ok(string localPath, long fileSize)
    {
        return new DownloadResult
        {
            Success = true,
            LocalPath = localPath,
            FileSize = fileSize
        };
    }

    public static DownloadResult Fail(string error)
    {
        return new DownloadResult
        {
            Success = false,
            Error = error
        };
    }
}

// Service for downloading files from Telegram
public class TelegramFileService
{
    private readonly string _tempPath;

    public TelegramFileService(string tempPath = "./temp")
    {
        _tempPath = tempPath;
        Directory.CreateDirectory(_tempPath);
    }

    // Download file from Telegram to local temp directory
    public async Task<DownloadResult> DownloadAsync(
        ITelegramBotClient bot,
        string fileId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(fileId))
        {
            return DownloadResult.Fail("File ID is empty");
        }

        try
        {
            // Get file info from Telegram
            var file = await bot.GetFile(fileId, ct);

            if (string.IsNullOrEmpty(file.FilePath))
            {
                return DownloadResult.Fail("File path not available");
            }

            // Generate local file path
            var extension = Path.GetExtension(file.FilePath);
            var localFileName = $"{Guid.NewGuid():N}{extension}";
            var localPath = Path.Combine(_tempPath, localFileName);

            // Download file
            using var stream = File.Create(localPath);
            await bot.DownloadFile(file.FilePath, stream, ct);

            Console.WriteLine($"[TelegramFileService] Downloaded: {localPath} ({file.FileSize} bytes)");

            return DownloadResult.Ok(localPath, file.FileSize ?? 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TelegramFileService] Download failed: {ex.Message}");
            return DownloadResult.Fail(ex.Message);
        }
    }

    // Clean up temp file after processing
    public void CleanupTempFile(string localPath)
    {
        if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath))
        {
            return;
        }

        try
        {
            File.Delete(localPath);
            Console.WriteLine($"[TelegramFileService] Cleaned up: {localPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TelegramFileService] Cleanup failed: {ex.Message}");
        }
    }

    // Clean up all temp files
    public void CleanupAllTempFiles()
    {
        try
        {
            if (Directory.Exists(_tempPath))
            {
                foreach (var file in Directory.GetFiles(_tempPath))
                {
                    File.Delete(file);
                }
                Console.WriteLine($"[TelegramFileService] Cleaned up all temp files");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TelegramFileService] Cleanup all failed: {ex.Message}");
        }
    }
}
