using System.Text.RegularExpressions;

namespace TelegramBotService.Analyzers;

// Text analyzer - equivalent to analyze_text.sh
public class TextAnalyzer : AnalyzerBase
{
    public override string Name => "TextAnalyzer";
    public override string Description => "Analyzes text content for character count, words, URLs, mentions, and hashtags";
    public override string[] SupportedContentTypes => new[] { "Text" };
    public override int Priority => 100;

    private static readonly Regex UrlPattern = new(@"https?://[^\s]+", RegexOptions.Compiled);
    private static readonly Regex MentionPattern = new(@"@[a-zA-Z0-9_]+", RegexOptions.Compiled);
    private static readonly Regex HashtagPattern = new(@"#[a-zA-Z0-9_]+", RegexOptions.Compiled);

    public override Task<AnalyzerResult> AnalyzeAsync(AnalyzerContext context, CancellationToken ct = default)
    {
        var input = !string.IsNullOrEmpty(context.Text) ? context.Text : context.Caption;

        if (string.IsNullOrEmpty(input))
        {
            return Task.FromResult(AnalyzerResult.Fail("No text content to analyze"));
        }

        var charCount = input.Length;
        var wordCount = CountWords(input);
        var lineCount = CountLines(input);
        var urlCount = UrlPattern.Matches(input).Count;
        var mentionCount = MentionPattern.Matches(input).Count;
        var hashtagCount = HashtagPattern.Matches(input).Count;

        var data = new Dictionary<string, object>
        {
            ["characters"] = charCount,
            ["words"] = wordCount,
            ["lines"] = lineCount,
            ["urls"] = urlCount,
            ["mentions"] = mentionCount,
            ["hashtags"] = hashtagCount
        };

        var result = $@"Text Analysis:
- Characters: {charCount}
- Words: {wordCount}
- Lines: {lineCount}
- URLs: {urlCount}
- Mentions: {mentionCount}
- Hashtags: {hashtagCount}";

        return Task.FromResult(AnalyzerResult.Ok(result, data));
    }

    private int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return text.Split('\n').Length;
    }
}
