using System.Text.Json.Serialization;

namespace ForwardAnalyzerBot.Models;

public class AnalysisInfo
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    public string Result { get; set; } = "";

    [JsonPropertyName("error")]
    public string Error { get; set; } = "";

    [JsonPropertyName("processingTimeMs")]
    public double ProcessingTimeMs { get; set; }
}
