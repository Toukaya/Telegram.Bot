namespace TelegramBotService.Storage;

// Configuration for file storage
public class FileStorageConfig
{
    public string BasePath { get; set; } = "./storage";
    public bool OrganizeByDate { get; set; } = true;      // Create date subdirectories
    public bool OrganizeByType { get; set; } = true;      // Create type subdirectories
    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024;  // 50MB default
}

// Result of file storage operation
public class StorageResult
{
    public bool Success { get; set; }
    public string LocalPath { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string Error { get; set; } = "";
    public long FileSize { get; set; }

    public static StorageResult Ok(string localPath, string relativePath, long size)
    {
        return new StorageResult
        {
            Success = true,
            LocalPath = localPath,
            RelativePath = relativePath,
            FileSize = size
        };
    }

    public static StorageResult Fail(string error)
    {
        return new StorageResult
        {
            Success = false,
            Error = error
        };
    }
}

// Service for storing and retrieving files on disk
public class FileStorage
{
    private readonly FileStorageConfig _config;
    private readonly string _basePath;

    public FileStorage(FileStorageConfig config = null)
    {
        _config = config ?? new FileStorageConfig();
        _basePath = Path.GetFullPath(_config.BasePath);

        EnsureDirectoryExists(_basePath);
    }

    // Store file from byte array
    public async Task<StorageResult> StoreAsync(
        byte[] data,
        string fileType,
        string extension,
        string fileId = null,
        CancellationToken ct = default)
    {
        if (data == null || data.Length == 0)
        {
            return StorageResult.Fail("No data provided");
        }

        if (data.Length > _config.MaxFileSizeBytes)
        {
            return StorageResult.Fail($"File size {data.Length} exceeds maximum {_config.MaxFileSizeBytes}");
        }

        try
        {
            var (localPath, relativePath) = GeneratePath(fileType, extension, fileId);
            EnsureDirectoryExists(Path.GetDirectoryName(localPath));

            await File.WriteAllBytesAsync(localPath, data, ct);

            return StorageResult.Ok(localPath, relativePath, data.Length);
        }
        catch (Exception ex)
        {
            return StorageResult.Fail($"Failed to store file: {ex.Message}");
        }
    }

    // Store file from stream
    public async Task<StorageResult> StoreAsync(
        Stream stream,
        string fileType,
        string extension,
        string fileId = null,
        CancellationToken ct = default)
    {
        if (stream == null || !stream.CanRead)
        {
            return StorageResult.Fail("Invalid stream");
        }

        try
        {
            var (localPath, relativePath) = GeneratePath(fileType, extension, fileId);
            EnsureDirectoryExists(Path.GetDirectoryName(localPath));

            long fileSize;
            using (var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write))
            {
                await stream.CopyToAsync(fileStream, ct);
                await fileStream.FlushAsync(ct);
                fileSize = fileStream.Length;
            }

            if (fileSize > _config.MaxFileSizeBytes)
            {
                File.Delete(localPath);
                return StorageResult.Fail($"File size {fileSize} exceeds maximum {_config.MaxFileSizeBytes}");
            }

            return StorageResult.Ok(localPath, relativePath, fileSize);
        }
        catch (Exception ex)
        {
            return StorageResult.Fail($"Failed to store file: {ex.Message}");
        }
    }

    // Copy existing file to storage
    public async Task<StorageResult> CopyAsync(
        string sourcePath,
        string fileType,
        string extension = null,
        string fileId = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(sourcePath))
        {
            return StorageResult.Fail($"Source file not found: {sourcePath}");
        }

        try
        {
            var ext = extension ?? Path.GetExtension(sourcePath);
            var (localPath, relativePath) = GeneratePath(fileType, ext, fileId);
            EnsureDirectoryExists(Path.GetDirectoryName(localPath));

            using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read);
            using var dest = new FileStream(localPath, FileMode.Create, FileAccess.Write);
            await source.CopyToAsync(dest, ct);

            var fileSize = new FileInfo(localPath).Length;
            return StorageResult.Ok(localPath, relativePath, fileSize);
        }
        catch (Exception ex)
        {
            return StorageResult.Fail($"Failed to copy file: {ex.Message}");
        }
    }

    // Read file as bytes
    public async Task<byte[]> ReadAsync(string path, CancellationToken ct = default)
    {
        var fullPath = GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(fullPath, ct);
    }

    // Read file as stream
    public Stream ReadStream(string path)
    {
        var fullPath = GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        return new FileStream(fullPath, FileMode.Open, FileAccess.Read);
    }

    // Check if file exists
    public bool Exists(string path)
    {
        return File.Exists(GetFullPath(path));
    }

    // Delete file
    public bool Delete(string path)
    {
        try
        {
            var fullPath = GetFullPath(path);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    // Get file info
    public FileInfo GetInfo(string path)
    {
        var fullPath = GetFullPath(path);
        if (File.Exists(fullPath))
        {
            return new FileInfo(fullPath);
        }
        return null;
    }

    // Get full path from relative path
    public string GetFullPath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }
        return Path.Combine(_basePath, path);
    }

    // Generate storage path based on config
    private (string LocalPath, string RelativePath) GeneratePath(string fileType, string extension, string fileId)
    {
        var parts = new List<string>();

        // Add type directory
        if (_config.OrganizeByType && !string.IsNullOrEmpty(fileType))
        {
            parts.Add(SanitizePathPart(fileType));
        }

        // Add date directory
        if (_config.OrganizeByDate)
        {
            parts.Add(DateTime.UtcNow.ToString("yyyy"));
            parts.Add(DateTime.UtcNow.ToString("MM"));
        }

        // Generate filename
        var id = fileId ?? Guid.NewGuid().ToString("N");
        var ext = extension.StartsWith(".") ? extension : $".{extension}";
        var filename = $"{id}{ext}";

        parts.Add(filename);

        var relativePath = Path.Combine(parts.ToArray());
        var localPath = Path.Combine(_basePath, relativePath);

        return (localPath, relativePath);
    }

    private string SanitizePathPart(string part)
    {
        var invalid = Path.GetInvalidPathChars();
        return string.Join("_", part.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    private void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    // Get storage statistics
    public StorageStats GetStats()
    {
        var stats = new StorageStats { BasePath = _basePath };

        if (Directory.Exists(_basePath))
        {
            var files = Directory.GetFiles(_basePath, "*", SearchOption.AllDirectories);
            stats.TotalFiles = files.Length;
            stats.TotalSizeBytes = files.Sum(f => new FileInfo(f).Length);

            // Count by type
            foreach (var typeDir in Directory.GetDirectories(_basePath))
            {
                var typeName = Path.GetFileName(typeDir);
                var typeFiles = Directory.GetFiles(typeDir, "*", SearchOption.AllDirectories);
                stats.FilesByType[typeName] = typeFiles.Length;
            }
        }

        return stats;
    }
}

public class StorageStats
{
    public string BasePath { get; set; } = "";
    public int TotalFiles { get; set; }
    public long TotalSizeBytes { get; set; }
    public Dictionary<string, int> FilesByType { get; set; } = new();

    public string TotalSizeFormatted
    {
        get
        {
            if (TotalSizeBytes < 1024) return $"{TotalSizeBytes} B";
            if (TotalSizeBytes < 1024 * 1024) return $"{TotalSizeBytes / 1024.0:F1} KB";
            if (TotalSizeBytes < 1024 * 1024 * 1024) return $"{TotalSizeBytes / (1024.0 * 1024):F1} MB";
            return $"{TotalSizeBytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }
}
