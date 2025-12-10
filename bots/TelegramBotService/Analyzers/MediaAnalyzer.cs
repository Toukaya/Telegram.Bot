namespace TelegramBotService.Analyzers;

// Media analyzer - equivalent to analyze_media.sh
public class MediaAnalyzer : AnalyzerBase
{
    public override string Name => "MediaAnalyzer";
    public override string Description => "Analyzes media content (Photo, Video, Voice, Audio, VideoNote, Sticker, Document)";
    public override string[] SupportedContentTypes => new[] { "Photo", "Video", "Voice", "Audio", "VideoNote", "Sticker", "Document" };
    public override int Priority => 100;

    public override Task<AnalyzerResult> AnalyzeAsync(AnalyzerContext context, CancellationToken ct = default)
    {
        var mediaType = context.ContentType;
        var fileId = context.FileId;
        var caption = context.Caption;

        var data = new Dictionary<string, object>
        {
            ["mediaType"] = mediaType,
            ["fileId"] = fileId,
            ["hasCaption"] = !string.IsNullOrEmpty(caption)
        };

        if (!string.IsNullOrEmpty(context.FileName))
        {
            data["fileName"] = context.FileName;
        }

        if (context.FileSize > 0)
        {
            data["fileSize"] = context.FileSize;
            data["fileSizeFormatted"] = FormatFileSize(context.FileSize);
        }

        if (!string.IsNullOrEmpty(context.MimeType))
        {
            data["mimeType"] = context.MimeType;
        }

        var lines = new List<string>
        {
            $"Media type: {mediaType}",
            $"File ID: {fileId}"
        };

        if (!string.IsNullOrEmpty(context.FileName))
        {
            lines.Add($"File name: {context.FileName}");
        }

        if (context.FileSize > 0)
        {
            lines.Add($"File size: {FormatFileSize(context.FileSize)}");
        }

        if (!string.IsNullOrEmpty(context.MimeType))
        {
            lines.Add($"MIME type: {context.MimeType}");
        }

        if (!string.IsNullOrEmpty(caption))
        {
            lines.Add($"Caption: {caption}");
        }

        lines.Add("");
        lines.Add("(Extended media analysis can be implemented here)");

        var result = string.Join("\n", lines);
        return Task.FromResult(AnalyzerResult.Ok(result, data));
    }

    private string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        double size = bytes;

        while (size >= 1024 && i < suffixes.Length - 1)
        {
            size /= 1024;
            i++;
        }

        return $"{size:0.##} {suffixes[i]}";
    }
}
