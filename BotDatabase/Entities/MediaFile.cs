namespace BotDatabase.Entities;

// Represents a media file stored in the system
// Links: File System (blob) <-> SQLite (metadata) <-> Kernel Memory (semantic)
public class MediaFile
{
    public string Id { get; set; } = "";               // GUID, also used as DocumentId in KM
    public string TelegramFileId { get; set; } = "";   // For re-download from Telegram
    public string TelegramFileUniqueId { get; set; } = "";  // For deduplication

    // Relationships
    public long ChatId { get; set; }
    public long UserId { get; set; }
    public long MessageId { get; set; }                // Telegram message ID

    // File Info
    public string FileType { get; set; } = "";         // audio/video/photo/voice/document
    public string MimeType { get; set; } = "";
    public string FileName { get; set; } = "";         // Original filename
    public long FileSize { get; set; }
    public string LocalPath { get; set; } = "";        // Relative path in storage

    // Text Conversion (Media-to-Text)
    public string TextContent { get; set; } = "";      // Transcription/OCR/Description
    public string ConvertStatus { get; set; } = MediaConvertStatus.Pending;
    public string ConvertError { get; set; } = "";
    public DateTime ConvertedAt { get; set; }

    // Semantic Index (Kernel Memory)
    public bool IsIndexed { get; set; }
    public DateTime IndexedAt { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation (optional, if you want to link to Message)
    public int DbMessageId { get; set; }               // FK to Message.Id (internal DB id)
}

public static class MediaConvertStatus
{
    public const string Pending = "pending";           // Waiting for conversion
    public const string Processing = "processing";    // Currently being processed
    public const string Completed = "completed";      // Successfully converted
    public const string Failed = "failed";            // Conversion failed
    public const string Skipped = "skipped";          // No conversion needed
    public const string Unavailable = "unavailable";  // Converter not available
}

public static class MediaFileType
{
    public const string Audio = "audio";
    public const string Voice = "voice";
    public const string Video = "video";
    public const string VideoNote = "video_note";
    public const string Photo = "photo";
    public const string Document = "document";
    public const string Sticker = "sticker";

    public static bool RequiresConversion(string type)
    {
        return type == Audio || type == Voice || type == Video ||
               type == VideoNote || type == Photo;
    }

    public static string GetConverterType(string type)
    {
        switch (type)
        {
            case Audio:
            case Voice:
                return "audio";
            case Video:
            case VideoNote:
                return "video";
            case Photo:
                return "photo";
            default:
                return null;
        }
    }
}
