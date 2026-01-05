using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ForwardAnalyzerBot.Services;

public class MessageTranscriber : IDisposable
{
    private const string Tag = "Transcriber";

    private readonly ITelegramBotClient _bot;
    private readonly string _outputPath;
    private readonly string _tempPath;
    private readonly string _whisperCliPath;
    private readonly string _whisperModelPath;

    private readonly BlockingCollection<Message> _messageQueue = new();
    private readonly Task _processingTask;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private int _messageIndex = 0;
    private bool _disposed;

    public MessageTranscriber(
        ITelegramBotClient bot,
        string outputPath,
        string tempPath = "./temp",
        string whisperCliPath = null,
        string whisperModelPath = null)
    {
        _bot = bot;
        _outputPath = outputPath;
        _tempPath = tempPath;

        // Default whisper paths
        _whisperCliPath = whisperCliPath ?? FindWhisperCli();
        _whisperModelPath = whisperModelPath ?? "/Users/touka/repo/whisper.cpp/models/ggml-medium.en.bin";

        // Ensure directories exist
        Directory.CreateDirectory(_tempPath);
        Directory.CreateDirectory(Path.GetDirectoryName(_outputPath) ?? ".");

        // Initialize output file with header
        var header = $"=== Message Transcription Log ===\nStarted: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n";
        System.IO.File.WriteAllText(_outputPath, header);

        Logger.Info(Tag, $"Output file: {_outputPath}");
        Logger.Info(Tag, $"Whisper CLI: {_whisperCliPath}");
        Logger.Info(Tag, $"Whisper Model: {_whisperModelPath}");

        // Start processing task
        _processingTask = Task.Run(ProcessQueueAsync);
    }

    private string FindWhisperCli()
    {
        var candidates = new[]
        {
            "/Users/touka/repo/whisper.cpp/build/bin/whisper-cli",
            "/Users/touka/repo/whisper.cpp/cmake-build-debug/bin/whisper-cli",
            "/usr/local/bin/whisper-cli"
        };

        foreach (var path in candidates)
        {
            if (System.IO.File.Exists(path))
            {
                return path;
            }
        }

        return candidates[0]; // default, will fail if not found
    }

    public void Enqueue(Message message)
    {
        if (_disposed) return;
        _messageQueue.Add(message);
        Logger.Debug(Tag, $"Message enqueued, queue size: {_messageQueue.Count}");
    }

    public int QueueCount => _messageQueue.Count;

    private async Task ProcessQueueAsync()
    {
        Logger.Info(Tag, "Message processing started");

        try
        {
            foreach (var message in _messageQueue.GetConsumingEnumerable(_cts.Token))
            {
                try
                {
                    await ProcessMessageAsync(message);
                }
                catch (Exception ex)
                {
                    Logger.Error(Tag, $"Error processing message {message.MessageId}", ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Debug(Tag, "Processing cancelled");
        }

        Logger.Info(Tag, "Message processing stopped");
    }

    private async Task ProcessMessageAsync(Message message)
    {
        var index = Interlocked.Increment(ref _messageIndex);
        string content;
        bool isAudio = false;

        if (message.Voice != null)
        {
            Logger.Info(Tag, $"Processing voice message #{index}");
            content = await TranscribeVoiceAsync(message);
            isAudio = true;
        }
        else if (message.Audio != null)
        {
            Logger.Info(Tag, $"Processing audio message #{index}");
            content = await TranscribeAudioAsync(message);
            isAudio = true;
        }
        else if (message.VideoNote != null)
        {
            Logger.Info(Tag, $"Processing video note #{index}");
            content = await TranscribeVideoNoteAsync(message);
            isAudio = true;
        }
        else if (message.Text != null)
        {
            content = message.Text;
        }
        else if (message.Caption != null)
        {
            content = message.Caption;
        }
        else if (message.Photo != null)
        {
            content = "[Photo]";
        }
        else if (message.Video != null)
        {
            content = "[Video]";
        }
        else if (message.Document != null)
        {
            var fileName = message.Document.FileName;
            content = fileName != null ? $"[Document: {fileName}]" : "[Document]";
        }
        else if (message.Sticker != null)
        {
            var emoji = message.Sticker.Emoji;
            content = emoji != null ? $"[Sticker: {emoji}]" : "[Sticker]";
        }
        else
        {
            content = "[Unknown message type]";
        }

        // Write to file
        await WriteToFileAsync(index, content, isAudio);
        Logger.Info(Tag, $"Message #{index} written to file");
    }

    private string GetSenderInfo(Message message)
    {
        var from = message.From;
        if (from == null) return "Unknown";

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(from.FirstName)) parts.Add(from.FirstName);
        if (!string.IsNullOrEmpty(from.LastName)) parts.Add(from.LastName);
        if (!string.IsNullOrEmpty(from.Username)) parts.Add($"@{from.Username}");

        return parts.Count > 0 ? string.Join(" ", parts) : $"User {from.Id}";
    }

    private async Task WriteToFileAsync(int index, string content, bool isAudio = false)
    {
        await _writeLock.WaitAsync();
        try
        {
            var entry = new StringBuilder();
            entry.AppendLine($"--- Message #{index} ---");
            if (isAudio)
            {
                entry.AppendLine("Audio Transcript:");
            }
            entry.AppendLine(content);
            entry.AppendLine();

            await System.IO.File.AppendAllTextAsync(_outputPath, entry.ToString());
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<string> TranscribeVoiceAsync(Message message)
    {
        return await TranscribeAudioFileAsync(message.Voice.FileId, message.Voice.FileUniqueId);
    }

    private async Task<string> TranscribeAudioAsync(Message message)
    {
        return await TranscribeAudioFileAsync(message.Audio.FileId, message.Audio.FileUniqueId);
    }

    private async Task<string> TranscribeVideoNoteAsync(Message message)
    {
        return await TranscribeAudioFileAsync(message.VideoNote.FileId, message.VideoNote.FileUniqueId);
    }

    private async Task<string> TranscribeAudioFileAsync(string fileId, string fileUniqueId)
    {
        var tempAudioPath = Path.Combine(_tempPath, $"{fileUniqueId}.ogg");
        var tempWavPath = Path.Combine(_tempPath, $"{fileUniqueId}.wav");

        try
        {
            // Download the file from Telegram
            Logger.Debug(Tag, $"Downloading audio file: {fileId}");
            var file = await _bot.GetFile(fileId);

            using (var stream = System.IO.File.Create(tempAudioPath))
            {
                await _bot.DownloadFile(file.FilePath, stream);
            }

            Logger.Debug(Tag, $"Downloaded to: {tempAudioPath}");

            // Convert to 16kHz mono WAV using ffmpeg
            Logger.Debug(Tag, "Converting to WAV...");
            var ffmpegResult = await RunProcessAsync(
                "ffmpeg",
                $"-y -hide_banner -loglevel error -i \"{tempAudioPath}\" -ar 16000 -ac 1 -c:a pcm_s16le \"{tempWavPath}\""
            );

            if (ffmpegResult.ExitCode != 0)
            {
                Logger.Error(Tag, $"ffmpeg failed: {ffmpegResult.Error}");
                return $"[Voice message - transcription failed: ffmpeg error]";
            }

            // Run whisper-cli
            Logger.Debug(Tag, "Running whisper transcription...");
            var whisperResult = await RunProcessAsync(
                _whisperCliPath,
                $"-m \"{_whisperModelPath}\" -f \"{tempWavPath}\" -np 1 -pp 0 -nt 1"
            );

            if (whisperResult.ExitCode != 0)
            {
                Logger.Error(Tag, $"whisper-cli failed: {whisperResult.Error}");
                return $"[Voice message - transcription failed: whisper error]";
            }

            var transcript = whisperResult.Output.Trim();
            if (string.IsNullOrWhiteSpace(transcript))
            {
                return "[no speech detected]";
            }

            return transcript;
        }
        catch (Exception ex)
        {
            Logger.Error(Tag, "Transcription failed", ex);
            return $"[Voice message - transcription failed: {ex.Message}]";
        }
        finally
        {
            // Cleanup temp files
            TryDelete(tempAudioPath);
            TryDelete(tempWavPath);
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
        catch
        {
            // ignore
        }
    }

    private async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(string program, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = program,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return (process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());
    }

    public async Task StopAsync()
    {
        Logger.Info(Tag, "Stopping transcriber...");
        _messageQueue.CompleteAdding();

        try
        {
            await _processingTask.WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException)
        {
            Logger.Warn(Tag, "Processing task did not complete in time");
            _cts.Cancel();
        }

        // Write footer
        await _writeLock.WaitAsync();
        try
        {
            var footer = $"\n=== End of Log ===\nStopped: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nTotal messages: {_messageIndex}\n";
            await System.IO.File.AppendAllTextAsync(_outputPath, footer);
        }
        finally
        {
            _writeLock.Release();
        }

        Logger.Info(Tag, $"Transcription complete. {_messageIndex} messages saved to {_outputPath}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _messageQueue.Dispose();
        _writeLock.Dispose();
        _cts.Dispose();
    }
}
