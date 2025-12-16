using System.Text.Json.Serialization;

namespace ForwardAnalyzerBot.Models;

public class ContentInfo
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("caption")]
    public string Caption { get; set; } = "";

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = "";

    [JsonPropertyName("fileId")]
    public string FileId { get; set; } = "";

    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }
}
