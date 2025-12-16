using System.Diagnostics;
using ForwardAnalyzerBot.Models;

namespace ForwardAnalyzerBot.Services;

public class ScriptRunner
{
    private readonly string _textScriptPath;
    private readonly string _mediaScriptPath;
    private readonly int _timeoutSeconds;

    public ScriptRunner(string textScriptPath, string mediaScriptPath, int timeoutSeconds = 30)
    {
        _textScriptPath = textScriptPath;
        _mediaScriptPath = mediaScriptPath;
        _timeoutSeconds = timeoutSeconds;
    }

    public bool IsTextScriptAvailable()
    {
        return File.Exists(_textScriptPath);
    }

    public bool IsMediaScriptAvailable()
    {
        return File.Exists(_mediaScriptPath);
    }

    public Task<AnalysisInfo> RunTextAnalysisAsync(string input)
    {
        return RunScriptAsync(_textScriptPath, input, null);
    }

    public Task<AnalysisInfo> RunMediaAnalysisAsync(string mediaType, string fileId, string caption)
    {
        // Pass media info as arguments: mediaType, fileId, caption
        var args = $"\"{mediaType}\" \"{fileId}\"";
        return RunScriptAsync(_mediaScriptPath, caption, args);
    }

    private async Task<AnalysisInfo> RunScriptAsync(string scriptPath, string stdinInput, string additionalArgs)
    {
        var stopwatch = Stopwatch.StartNew();
        var analysisInfo = new AnalysisInfo();

        if (!File.Exists(scriptPath))
        {
            stopwatch.Stop();
            analysisInfo.Success = false;
            analysisInfo.Error = $"Script not found: {scriptPath}";
            analysisInfo.ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            return analysisInfo;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = scriptPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (!string.IsNullOrEmpty(additionalArgs))
            {
                psi.Arguments = additionalArgs;
            }
            else if (!string.IsNullOrEmpty(stdinInput))
            {
                var escapedInput = stdinInput.Replace("\"", "\\\"").Replace("\n", "\\n");
                psi.Arguments = $"\"{escapedInput}\"";
            }

            using var process = new Process();
            process.StartInfo = psi;
            process.Start();

            // Write input to stdin
            if (!string.IsNullOrEmpty(stdinInput))
            {
                await process.StandardInput.WriteAsync(stdinInput);
            }
            process.StandardInput.Close();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            var completed = await Task.Run(() => process.WaitForExit(_timeoutSeconds * 1000));

            stopwatch.Stop();

            if (!completed)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore kill errors
                }

                analysisInfo.Success = false;
                analysisInfo.Error = $"Script execution timed out after {_timeoutSeconds} seconds";
                analysisInfo.ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                return analysisInfo;
            }

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                analysisInfo.Success = false;
                analysisInfo.Error = !string.IsNullOrEmpty(error) ? error : $"Script exited with code {process.ExitCode}";
                analysisInfo.Result = output;
                analysisInfo.ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                return analysisInfo;
            }

            analysisInfo.Success = true;
            analysisInfo.Result = output.Trim();
            analysisInfo.ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            return analysisInfo;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            analysisInfo.Success = false;
            analysisInfo.Error = $"Script execution failed: {ex.Message}";
            analysisInfo.ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            return analysisInfo;
        }
    }
}
