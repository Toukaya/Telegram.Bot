using System.Text.Json.Serialization;

namespace ForwardAnalyzerBot.Models;

public class SenderInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = "";
}
