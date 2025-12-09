using System.Threading.Channels;

namespace ForwardAnalyzerBot.Services;

public class TaskPool : IDisposable
{
    private readonly Channel<Func<Task>> _channel;
    private readonly Task[] _processors;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public TaskPool(int concurrency = 1, int capacity = 100)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = concurrency == 1,
            SingleWriter = false
        };

        _channel = Channel.CreateBounded<Func<Task>>(options);

        _processors = new Task[concurrency];
        for (int i = 0; i < concurrency; i++)
        {
            _processors[i] = ProcessQueueAsync(_cts.Token);
        }
    }

    public async Task EnqueueAsync(Func<Task> task)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TaskPool));
        }

        await _channel.Writer.WriteAsync(task);
    }

    public bool TryEnqueue(Func<Task> task)
    {
        if (_disposed)
        {
            return false;
        }

        return _channel.Writer.TryWrite(task);
    }

    public int PendingCount => _channel.Reader.Count;

    private async Task ProcessQueueAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var task in _channel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await task();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TaskPool] Task execution failed: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation, ignore
        }
    }

    public async Task StopAsync()
    {
        _channel.Writer.Complete();

        try
        {
            await Task.WhenAll(_processors);
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation exceptions
        }
    }

    public void Cancel()
    {
        _cts.Cancel();
        _channel.Writer.Complete();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Cancel();
        _cts.Dispose();
    }
}
