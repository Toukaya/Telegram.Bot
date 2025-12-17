using System.Diagnostics;
using System.Text;

namespace TelegramBotService.MediaConverters;

// Converts audio/voice files to text using Whisper CLI
public class AudioConverter : MediaConverterBase
{
    private readonly string _whisperPath;
    private readonly string _whisperModel;
    private readonly int _timeoutSeconds;
    private bool _isAvailable;

    public override string Name => "AudioConverter";
    public override string[] SupportedContentTypes => new[] { "audio", "voice" };
    public override bool IsAvailable => _isAvailable;
    public override int Priority => 10;

    public AudioConverter(string whisperPath = "whisper", string model = "base", int timeoutSeconds = 300)
    {
        _whisperPath = whisperPath;
        _whisperModel = model;
        _timeoutSeconds = timeoutSeconds;
        _isAvailable = CheckWhisperAvailable();
    }

    private bool CheckWhisperAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _whisperPath,
                Arguments = "--help",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            process.WaitForExit(5000);
            return process.ExitCode == 0 || process.ExitCode == 2;  // --help may return 2
        }
        catch
        {
            return false;
        }
    }

    public override async Task<ConversionResult> ConvertAsync(ConversionContext context, CancellationToken ct = default)
    {
        if (!_isAvailable)
        {
            return ConversionResult.Unavailable("Whisper is not available");
        }

        if (string.IsNullOrEmpty(context.FilePath) || !File.Exists(context.FilePath))
        {
            return ConversionResult.Fail("File path is required and must exist");
        }

        var sw = Stopwatch.StartNew();
        var outputDir = Path.GetTempPath();
        var outputBase = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(context.FilePath));

        try
        {
            // Run whisper
            var args = $"\"{context.FilePath}\" --model {_whisperModel} --output_format txt --output_dir \"{outputDir}\"";

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
            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var completed = await Task.Run(() => process.WaitForExit(_timeoutSeconds * 1000), ct);

            if (!completed)
            {
                process.Kill();
                return ConversionResult.Fail($"Whisper timed out after {_timeoutSeconds} seconds");
            }

            sw.Stop();

            if (process.ExitCode != 0)
            {
                return ConversionResult.Fail($"Whisper failed with exit code {process.ExitCode}: {error}");
            }

            // Read the output text file
            var txtFile = outputBase + ".txt";
            if (!File.Exists(txtFile))
            {
                return ConversionResult.Fail("Whisper did not produce output file");
            }

            var text = await File.ReadAllTextAsync(txtFile, ct);

            // Clean up temp file
            try { File.Delete(txtFile); } catch { }

            var result = ConversionResult.Ok(text.Trim(), new Dictionary<string, object>
            {
                ["model"] = _whisperModel,
                ["source_file"] = context.FileName
            });
            result.ProcessingTimeMs = sw.ElapsedMilliseconds;

            return result;
        }
        catch (Exception ex)
        {
            return ConversionResult.Fail($"Audio conversion error: {ex.Message}");
        }
    }
}
