using System.Diagnostics;
using System.Text;

namespace TelegramBotService.MediaConverters;

// Converts video files to text by extracting audio and using Whisper
public class VideoConverter : MediaConverterBase
{
    private readonly string _ffmpegPath;
    private readonly string _whisperPath;
    private readonly string _whisperModel;
    private readonly int _timeoutSeconds;
    private bool _isFfmpegAvailable;
    private bool _isWhisperAvailable;

    public override string Name => "VideoConverter";
    public override string[] SupportedContentTypes => new[] { "video", "video_note" };
    public override bool IsAvailable => _isFfmpegAvailable && _isWhisperAvailable;
    public override int Priority => 10;

    public VideoConverter(
        string ffmpegPath = "ffmpeg",
        string whisperPath = "whisper",
        string whisperModel = "base",
        int timeoutSeconds = 600)
    {
        _ffmpegPath = ffmpegPath;
        _whisperPath = whisperPath;
        _whisperModel = whisperModel;
        _timeoutSeconds = timeoutSeconds;

        _isFfmpegAvailable = CheckToolAvailable(_ffmpegPath, "-version");
        _isWhisperAvailable = CheckToolAvailable(_whisperPath, "--help");
    }

    private bool CheckToolAvailable(string tool, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = tool,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            process.WaitForExit(5000);
            return true;  // If it runs at all, the tool exists
        }
        catch
        {
            return false;
        }
    }

    public override async Task<ConversionResult> ConvertAsync(ConversionContext context, CancellationToken ct = default)
    {
        if (!_isFfmpegAvailable)
        {
            return ConversionResult.Unavailable("FFmpeg is not available");
        }

        if (!_isWhisperAvailable)
        {
            return ConversionResult.Unavailable("Whisper is not available");
        }

        if (string.IsNullOrEmpty(context.FilePath) || !File.Exists(context.FilePath))
        {
            return ConversionResult.Fail("File path is required and must exist");
        }

        var sw = Stopwatch.StartNew();
        var tempAudioFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.wav");

        try
        {
            // Step 1: Extract audio from video using FFmpeg
            var extractResult = await ExtractAudioAsync(context.FilePath, tempAudioFile, ct);
            if (!extractResult.Success)
            {
                return extractResult;
            }

            // Step 2: Transcribe audio using Whisper
            var transcribeResult = await TranscribeAudioAsync(tempAudioFile, ct);

            sw.Stop();
            transcribeResult.ProcessingTimeMs = sw.ElapsedMilliseconds;

            if (transcribeResult.Success)
            {
                transcribeResult.Metadata["source_type"] = "video";
                transcribeResult.Metadata["source_file"] = context.FileName;
            }

            return transcribeResult;
        }
        finally
        {
            // Clean up temp audio file
            try { File.Delete(tempAudioFile); } catch { }
        }
    }

    private async Task<ConversionResult> ExtractAudioAsync(string videoPath, string audioPath, CancellationToken ct)
    {
        // ffmpeg -i input.mp4 -vn -acodec pcm_s16le -ar 16000 -ac 1 output.wav
        var args = $"-i \"{videoPath}\" -vn -acodec pcm_s16le -ar 16000 -ac 1 -y \"{audioPath}\"";

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var error = new StringBuilder();

        process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.AppendLine(e.Data); };

        process.Start();
        process.BeginErrorReadLine();

        var completed = await Task.Run(() => process.WaitForExit(120 * 1000), ct);  // 2 min for audio extraction

        if (!completed)
        {
            process.Kill();
            return ConversionResult.Fail("FFmpeg audio extraction timed out");
        }

        if (!File.Exists(audioPath) || new FileInfo(audioPath).Length == 0)
        {
            // Video might have no audio track
            return ConversionResult.Ok("", new Dictionary<string, object> { ["no_audio"] = true });
        }

        return ConversionResult.Ok("");  // Success, continue to transcription
    }

    private async Task<ConversionResult> TranscribeAudioAsync(string audioPath, CancellationToken ct)
    {
        var outputDir = Path.GetTempPath();
        var outputBase = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(audioPath));

        var args = $"\"{audioPath}\" --model {_whisperModel} --output_format txt --output_dir \"{outputDir}\"";

        var psi = new ProcessStartInfo
        {
            FileName = _whisperPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var error = new StringBuilder();

        process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.AppendLine(e.Data); };

        process.Start();
        process.BeginErrorReadLine();

        var completed = await Task.Run(() => process.WaitForExit(_timeoutSeconds * 1000), ct);

        if (!completed)
        {
            process.Kill();
            return ConversionResult.Fail($"Whisper timed out after {_timeoutSeconds} seconds");
        }

        if (process.ExitCode != 0)
        {
            return ConversionResult.Fail($"Whisper failed: {error}");
        }

        var txtFile = outputBase + ".txt";
        if (!File.Exists(txtFile))
        {
            return ConversionResult.Fail("Whisper did not produce output");
        }

        var text = await File.ReadAllTextAsync(txtFile, ct);

        try { File.Delete(txtFile); } catch { }

        return ConversionResult.Ok(text.Trim(), new Dictionary<string, object>
        {
            ["model"] = _whisperModel
        });
    }
}
