namespace BotDatabase.Entities;

public class Message
{
    public int Id { get; set; }
    public long TelegramMessageId { get; set; }
    public long ChatId { get; set; }
    public long UserId { get; set; }
    public string Content { get; set; } = "";
    public string ContentType { get; set; } = "text";  // text, photo, video, audio, voice, document
    public string FileId { get; set; } = "";
    public DateTime SentAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Forward info
    public int ForwardSourceId { get; set; }

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
}
