using System.Text.Json.Serialization;

namespace ForwardAnalyzerBot.Models;

public class SourceInfo
{
    [JsonPropertyName("originalDate")]
    public DateTime OriginalDate { get; set; }

    [JsonPropertyName("forwardDate")]
    public DateTime ForwardDate { get; set; }

    [JsonPropertyName("messageLink")]
    public string MessageLink { get; set; } = "";

    [JsonPropertyName("originalMessageId")]
    public int OriginalMessageId { get; set; }

    [JsonPropertyName("chatTitle")]
    public string ChatTitle { get; set; } = "";

    [JsonPropertyName("chatId")]
    public long ChatId { get; set; }
}
