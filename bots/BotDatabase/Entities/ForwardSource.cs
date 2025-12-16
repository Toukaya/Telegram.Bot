namespace BotDatabase.Entities;

public class ForwardSource
{
    public int Id { get; set; }
    public string OriginType { get; set; } = "";   // user, hidden_user, chat, channel
    public long OriginId { get; set; }
    public string OriginName { get; set; } = "";
    public string OriginUsername { get; set; } = "";
    public DateTime OriginalDate { get; set; }
    public string MessageLink { get; set; } = "";

    // Navigation
    public Message Message { get; set; }
}

public static class OriginTypes
{
    public const string User = "user";
    public const string HiddenUser = "hidden_user";
    public const string Chat = "chat";
    public const string Channel = "channel";
}
