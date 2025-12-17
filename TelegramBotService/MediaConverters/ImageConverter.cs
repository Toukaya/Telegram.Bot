using System.Diagnostics;
using System.Text;

namespace TelegramBotService.MediaConverters;

// Converts images to text using Tesseract OCR
public class ImageConverter : MediaConverterBase
{
    private readonly string _tesseractPath;
    private readonly string _language;
    private readonly int _timeoutSeconds;
    private bool _isAvailable;

    public override string Name => "ImageConverter";
    public override string[] SupportedContentTypes => new[] { "photo", "sticker" };
    public override bool IsAvailable => _isAvailable;
    public override int Priority => 10;

    public ImageConverter(string tesseractPath = "tesseract", string language = "eng+chi_sim+jpn", int timeoutSeconds = 60)
    {
        _tesseractPath = tesseractPath;
        _language = language;
        _timeoutSeconds = timeoutSeconds;
        _isAvailable = CheckTesseractAvailable();
    }

    private bool CheckTesseractAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _tesseractPath,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            process.WaitForExit(5000);
            return process.ExitCode == 0;
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
            return ConversionResult.Unavailable("Tesseract is not available");
        }

        if (string.IsNullOrEmpty(context.FilePath) || !File.Exists(context.FilePath))
        {
            return ConversionResult.Fail("File path is required and must exist");
        }

        var sw = Stopwatch.StartNew();
        var outputFile = Path.GetTempFileName();

        try
        {
            // Run tesseract: tesseract input.png output -l eng+chi_sim
            var args = $"\"{context.FilePath}\" \"{outputFile}\" -l {_language}";

            var psi = new ProcessStartInfo
            {
                FileName = _tesseractPath,
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
                return ConversionResult.Fail($"Tesseract timed out after {_timeoutSeconds} seconds");
            }

            sw.Stop();

            if (process.ExitCode != 0)
            {
                return ConversionResult.Fail($"Tesseract failed with exit code {process.ExitCode}: {error}");
            }

            // Tesseract adds .txt extension
            var txtFile = outputFile + ".txt";
            if (!File.Exists(txtFile))
            {
                return ConversionResult.Fail("Tesseract did not produce output file");
            }

            var text = await File.ReadAllTextAsync(txtFile, ct);

            // Clean up temp files
            try { File.Delete(outputFile); } catch { }
            try { File.Delete(txtFile); } catch { }

            // If no text was extracted, return empty success (not failure)
            var trimmedText = text.Trim();

            var result = ConversionResult.Ok(trimmedText, new Dictionary<string, object>
            {
                ["language"] = _language,
                ["has_text"] = !string.IsNullOrEmpty(trimmedText),
                ["source_file"] = context.FileName
            });
            result.ProcessingTimeMs = sw.ElapsedMilliseconds;

            return result;
        }
        catch (Exception ex)
        {
            return ConversionResult.Fail($"Image conversion error: {ex.Message}");
        }
        finally
        {
            // Ensure cleanup
            try { File.Delete(outputFile); } catch { }
            try { File.Delete(outputFile + ".txt"); } catch { }
        }
    }
}
