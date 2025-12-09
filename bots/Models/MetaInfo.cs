using System.Text.Json.Serialization;

namespace ForwardAnalyzerBot.Models;

public class MetaInfo
{
    [JsonPropertyName("processedAt")]
    public DateTime ProcessedAt { get; set; }

    [JsonPropertyName("botVersion")]
    public string BotVersion { get; set; } = "1.0.0";

    [JsonPropertyName("receivedFromChatId")]
    public long ReceivedFromChatId { get; set; }

    [JsonPropertyName("receivedFromUserId")]
    public long ReceivedFromUserId { get; set; }
}
