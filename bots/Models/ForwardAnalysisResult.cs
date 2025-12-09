using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForwardAnalyzerBot.Models;

public class ForwardAnalysisResult
{
    [JsonPropertyName("sender")]
    public SenderInfo Sender { get; set; } = new();

    [JsonPropertyName("source")]
    public SourceInfo Source { get; set; } = new();

    [JsonPropertyName("content")]
    public ContentInfo Content { get; set; } = new();

    [JsonPropertyName("analysis")]
    public AnalysisInfo Analysis { get; set; } = new();

    [JsonPropertyName("meta")]
    public MetaInfo Meta { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string ToJson(bool indented = true)
    {
        if (indented)
        {
            return JsonSerializer.Serialize(this, JsonOptions);
        }

        var compactOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        return JsonSerializer.Serialize(this, compactOptions);
    }
}
