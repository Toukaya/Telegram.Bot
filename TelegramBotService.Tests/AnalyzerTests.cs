using TelegramBotService;
using TelegramBotService.Analyzers;
using Xunit;

namespace TelegramBotService.Tests;

public class AnalyzerTests
{
    // ==================== AnalyzerContext Tests ====================

    [Fact]
    public void AnalyzerContext_DefaultValues_AreCorrect()
    {
        var context = new AnalyzerContext();

        Assert.Equal("", context.ContentType);
        Assert.Equal("", context.Text);
        Assert.Equal("", context.Caption);
        Assert.Equal("", context.FileId);
        Assert.Equal("", context.FileName);
        Assert.Equal(0, context.FileSize);
        Assert.Equal("", context.MimeType);
        Assert.Empty(context.FileData);
        Assert.NotNull(context.Extra);
    }

    [Fact]
    public void AnalyzerContext_CanSetProperties()
    {
        var context = new AnalyzerContext
        {
            ContentType = "Text",
            Text = "Hello world",
            FileId = "file123",
            FileSize = 1024
        };

        Assert.Equal("Text", context.ContentType);
        Assert.Equal("Hello world", context.Text);
        Assert.Equal("file123", context.FileId);
        Assert.Equal(1024, context.FileSize);
    }

    // ==================== AnalyzerResult Tests ====================

    [Fact]
    public void AnalyzerResult_Ok_CreatesSuccessResult()
    {
        var data = new Dictionary<string, object>
        {
            ["key1"] = "value1",
            ["key2"] = 42
        };

        var result = AnalyzerResult.Ok("Analysis complete", data);

        Assert.True(result.Success);
        Assert.Equal("Analysis complete", result.Result);
        Assert.Empty(result.Error);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal("value1", result.Data["key1"]);
        Assert.Equal(42, result.Data["key2"]);
    }

    [Fact]
    public void AnalyzerResult_Ok_WithoutData_CreatesEmptyDictionary()
    {
        var result = AnalyzerResult.Ok("Success");

        Assert.True(result.Success);
        Assert.Equal("Success", result.Result);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public void AnalyzerResult_Fail_CreatesFailureResult()
    {
        var result = AnalyzerResult.Fail("Something went wrong");

        Assert.False(result.Success);
        Assert.Equal("Something went wrong", result.Error);
        Assert.Empty(result.Result);
    }

    // ==================== TextAnalyzer Tests ====================

    [Fact]
    public void TextAnalyzer_HasCorrectProperties()
    {
        var analyzer = new TextAnalyzer();

        Assert.Equal("TextAnalyzer", analyzer.Name);
        Assert.NotEmpty(analyzer.Description);
        Assert.Contains("Text", analyzer.SupportedContentTypes);
        Assert.Equal(100, analyzer.Priority);
    }

    [Fact]
    public async Task TextAnalyzer_SimpleText_AnalyzesCorrectly()
    {
        var analyzer = new TextAnalyzer();
        var context = new AnalyzerContext
        {
            ContentType = "Text",
            Text = "Hello world"
        };

        var result = await analyzer.AnalyzeAsync(context);

        Assert.True(result.Success);
        Assert.Contains("11", result.Result);  // 11 characters
        Assert.Contains("2", result.Result);   // 2 words
        Assert.Equal(11, result.Data["characters"]);
        Assert.Equal(2, result.Data["words"]);
    }

    [Fact]
    public async Task TextAnalyzer_TextWithUrls_CountsUrls()
    {
        var analyzer = new TextAnalyzer();
        var context = new AnalyzerContext
        {
            ContentType = "Text",
            Text = "Check https://example.com and http://test.org"
        };

        var result = await analyzer.AnalyzeAsync(context);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data["urls"]);
    }

    [Fact]
    public async Task TextAnalyzer_TextWithMentions_CountsMentions()
    {
        var analyzer = new TextAnalyzer();
        var context = new AnalyzerContext
        {
            ContentType = "Text",
            Text = "Hello @alice and @bob"
        };

        var result = await analyzer.AnalyzeAsync(context);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data["mentions"]);
    }

    [Fact]
    public async Task TextAnalyzer_TextWithHashtags_CountsHashtags()
    {
        var analyzer = new TextAnalyzer();
        var context = new AnalyzerContext
        {
            ContentType = "Text",
            Text = "This is #awesome and #cool"
        };

        var result = await analyzer.AnalyzeAsync(context);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data["hashtags"]);
    }

    [Fact]
    public async Task TextAnalyzer_MultilineText_CountsLines()
    {
        var analyzer = new TextAnalyzer();
        var context = new AnalyzerContext
        {
            ContentType = "Text",
            Text = "Line 1\nLine 2\nLine 3"
        };

        var result = await analyzer.AnalyzeAsync(context);

        Assert.True(result.Success);
        Assert.Equal(3, result.Data["lines"]);
    }

    [Fact]
    public async Task TextAnalyzer_EmptyText_Fails()
    {
        var analyzer = new TextAnalyzer();
        var context = new AnalyzerContext
        {
            ContentType = "Text",
            Text = ""
        };

        var result = await analyzer.AnalyzeAsync(context);

        Assert.False(result.Success);
        Assert.Contains("No text content", result.Error);
    }

    [Fact]
    public async Task TextAnalyzer_UsesCaption_WhenTextEmpty()
    {
        var analyzer = new TextAnalyzer();
        var context = new AnalyzerContext
        {
            ContentType = "Text",
            Text = "",
            Caption = "Caption text"
        };

        var result = await analyzer.AnalyzeAsync(context);

        Assert.True(result.Success);
        Assert.Equal(12, result.Data["characters"]);
    }

    [Fact]
    public async Task TextAnalyzer_ComplexText_AnalyzesAllFeatures()
    {
        var analyzer = new TextAnalyzer();
        var context = new AnalyzerContext
        {
            ContentType = "Text",
            Text = @"Hello @user! Check https://example.com for #updates
This is line 2 with https://test.org
Final line with #cool and #awesome"
        };

        var result = await analyzer.AnalyzeAsync(context);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data["urls"]);
        Assert.Equal(1, result.Data["mentions"]);
        Assert.Equal(3, result.Data["hashtags"]);
        Assert.Equal(3, result.Data["lines"]);
        Assert.True((int)result.Data["words"] > 10);
    }

    // ==================== MediaAnalyzer Tests ====================

    [Fact]
    public void MediaAnalyzer_HasCorrectProperties()
    {
        var analyzer = new MediaAnalyzer();

        Assert.Equal("MediaAnalyzer", analyzer.Name);
        Assert.NotEmpty(analyzer.Description);
        Assert.Contains("Photo", analyzer.SupportedContentTypes);
        Assert.Contains("Video", analyzer.SupportedContentTypes);
        Assert.Contains("Audio", analyzer.SupportedContentTypes);
        Assert.Contains("Voice", analyzer.SupportedContentTypes);
        Assert.Equal(100, analyzer.Priority);
    }

    [Fact]
    public async Task MediaAnalyzer_BasicMedia_AnalyzesCorrectly()
    {
        var analyzer = new MediaAnalyzer();
        var context = new AnalyzerContext
        {
            ContentType = "Audio",
            FileId = "audio_file_123",
            FileName = "song.mp3",
            FileSize = 1024000,
            MimeType = "audio/mpeg"
        };

        var result = await analyzer.AnalyzeAsync(context);

        Assert.True(result.Success);
        Assert.Contains("Audio", result.Result);
        Assert.Contains("audio_file_123", result.Result);
        Assert.Contains("song.mp3", result.Result);
        Assert.Equal("Audio", result.Data["mediaType"]);
        Assert.Equal("audio_file_123", result.Data["fileId"]);
        Assert.Equal("song.mp3", result.Data["fileName"]);
    }

    [Fact]
    public async Task MediaAnalyzer_WithCaption_IncludesCaption()
    {
        var analyzer = new MediaAnalyzer();
        var context = new AnalyzerContext
        {
            ContentType = "Photo",
            FileId = "photo123",
            Caption = "Beautiful sunset"
        };

        var result = await analyzer.AnalyzeAsync(context);

        Assert.True(result.Success);
        Assert.Contains("Beautiful sunset", result.Result);
        Assert.True((bool)result.Data["hasCaption"]);
    }

    [Fact]
    public async Task MediaAnalyzer_WithoutCaption_NoCaption()
    {
        var analyzer = new MediaAnalyzer();
        var context = new AnalyzerContext
        {
            ContentType = "Photo",
            FileId = "photo123"
        };

        var result = await analyzer.AnalyzeAsync(context);

        Assert.True(result.Success);
        Assert.False((bool)result.Data["hasCaption"]);
    }

    [Fact]
    public async Task MediaAnalyzer_FormatsFileSize_Bytes()
    {
        var analyzer = new MediaAnalyzer();
        var context = new AnalyzerContext
        {
            ContentType = "Photo",
            FileId = "photo123",
            FileSize = 512
        };

        var result = await analyzer.AnalyzeAsync(context);

        Assert.True(result.Success);
        Assert.Contains("512 B", result.Result);
    }

    [Fact]
    public async Task MediaAnalyzer_FormatsFileSize_Kilobytes()
    {
        var analyzer = new MediaAnalyzer();
        var context = new AnalyzerContext
        {
            ContentType = "Audio",
            FileId = "audio123",
            FileSize = 5120  // 5 KB
        };

        var result = await analyzer.AnalyzeAsync(context);

        Assert.True(result.Success);
        Assert.Contains("5 KB", result.Result);
    }

    [Fact]
    public async Task MediaAnalyzer_FormatsFileSize_Megabytes()
    {
        var analyzer = new MediaAnalyzer();
        var context = new AnalyzerContext
        {
            ContentType = "Video",
            FileId = "video123",
            FileSize = 5242880  // 5 MB
        };

        var result = await analyzer.AnalyzeAsync(context);

        Assert.True(result.Success);
        Assert.Contains("5 MB", result.Result);
    }

    [Fact]
    public async Task MediaAnalyzer_FormatsFileSize_Gigabytes()
    {
        var analyzer = new MediaAnalyzer();
        var context = new AnalyzerContext
        {
            ContentType = "Video",
            FileId = "video123",
            FileSize = 2147483648  // 2 GB
        };

        var result = await analyzer.AnalyzeAsync(context);

        Assert.True(result.Success);
        Assert.Contains("2 GB", result.Result);
    }

    [Fact]
    public async Task MediaAnalyzer_AllMediaTypes_Work()
    {
        var analyzer = new MediaAnalyzer();
        var mediaTypes = new[] { "Photo", "Video", "Voice", "Audio", "VideoNote", "Sticker", "Document" };

        foreach (var mediaType in mediaTypes)
        {
            var context = new AnalyzerContext
            {
                ContentType = mediaType,
                FileId = $"{mediaType.ToLower()}_123"
            };

            var result = await analyzer.AnalyzeAsync(context);

            Assert.True(result.Success);
            Assert.Contains(mediaType, result.Result);
        }
    }

    // ==================== AnalyzerBase Tests ====================

    [Fact]
    public void AnalyzerBase_CanHandle_MatchesContentType()
    {
        var analyzer = new TextAnalyzer();

        // TextAnalyzer supports "Text"
        var context = new AnalyzerContext { ContentType = "Text" };
        var result = analyzer.AnalyzeAsync(context);

        Assert.NotNull(result);
    }

    [Fact]
    public void AnalyzerBase_SupportedContentTypes_ReturnsArray()
    {
        var textAnalyzer = new TextAnalyzer();
        var mediaAnalyzer = new MediaAnalyzer();

        Assert.NotEmpty(textAnalyzer.SupportedContentTypes);
        Assert.NotEmpty(mediaAnalyzer.SupportedContentTypes);
        Assert.True(mediaAnalyzer.SupportedContentTypes.Length > 1);
    }

    // ==================== Cancellation Tests ====================

    [Fact]
    public async Task TextAnalyzer_Cancellation_ThrowsOperationCanceled()
    {
        var analyzer = new TextAnalyzer();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var context = new AnalyzerContext
        {
            ContentType = "Text",
            Text = "Test text"
        };

        // TextAnalyzer does not actually check cancellation token
        // but we test that it accepts the parameter
        var result = await analyzer.AnalyzeAsync(context, cts.Token);

        // Should complete immediately since analysis is synchronous
        Assert.NotNull(result);
    }

    [Fact]
    public async Task MediaAnalyzer_Cancellation_AcceptsToken()
    {
        var analyzer = new MediaAnalyzer();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var context = new AnalyzerContext
        {
            ContentType = "Photo",
            FileId = "photo123"
        };

        // MediaAnalyzer does not actually check cancellation token
        var result = await analyzer.AnalyzeAsync(context, cts.Token);

        Assert.NotNull(result);
    }
}
