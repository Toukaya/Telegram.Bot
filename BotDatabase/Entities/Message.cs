namespace BotDatabase.Entities;

public class Message
{
    public int Id { get; set; }
    public long TelegramMessageId { get; set; }
    public long ChatId { get; set; }
    public long UserId { get; set; }
    public string Content { get; set; } = "";
    public string ContentType { get; set; } = "text";  // text, photo, video, audio, voice, document
    public DateTime SentAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Forward info
    public int ForwardSourceId { get; set; }

    // Media storage
    public string FileId { get; set; } = "";           // Telegram file_id for re-download
    public string FileUniqueId { get; set; } = "";     // Telegram file_unique_id for deduplication
    public string LocalPath { get; set; } = "";        // Local file path (if downloaded)
    public long FileSize { get; set; }                 // File size in bytes
    public string MimeType { get; set; } = "";         // MIME type
    public string FileName { get; set; } = "";         // Original filename

    // Media-to-text conversion (non-AI)
    public string ConversionStatus { get; set; } = ConversionStatuses.Pending;  // pending, completed, failed, skipped
    public string ConvertedText { get; set; } = "";    // Transcribed/OCR text from media
    public string ConversionError { get; set; } = "";  // Error message if conversion failed
    public DateTime ConvertedAt { get; set; }          // When conversion completed

    // Navigation
    public Chat Chat { get; set; }
    public User User { get; set; }
    public ForwardSource ForwardSource { get; set; }
    public AnalysisResult AnalysisResult { get; set; }
}

public static class ContentTypes
{
    public const string Text = "text";
    public const string Photo = "photo";
    public const string Video = "video";
    public const string Audio = "audio";
    public const string Voice = "voice";
    public const string Document = "document";
    public const string VideoNote = "video_note";
    public const string Sticker = "sticker";
}

public static class ConversionStatuses
{
    public const string Pending = "pending";       // Waiting for conversion
    public const string Completed = "completed";   // Successfully converted to text
    public const string Failed = "failed";         // Conversion failed
    public const string Skipped = "skipped";       // No conversion needed (e.g., text message)
    public const string Unavailable = "unavailable"; // Conversion service not available
}
