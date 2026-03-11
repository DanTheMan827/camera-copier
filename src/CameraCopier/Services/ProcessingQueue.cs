using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace DanTheMan827.CameraCopier.Services;

/// <summary>
/// A thread-safe queue for file paths awaiting processing.
/// Prevents duplicate entries and allows tracking of active item count.
/// </summary>
public interface IProcessingQueue
{
    /// <summary>
    /// Gets the number of files currently being actively processed.
    /// </summary>
    int ActiveCount { get; }

    /// <summary>
    /// Attempts to enqueue a file path. Returns false if already queued.
    /// </summary>
    bool TryEnqueue(string filePath);

    /// <summary>
    /// Reads the next file path from the queue asynchronously.
    /// </summary>
    ValueTask<string> DequeueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Signals that a file has finished processing (success or failure).
    /// </summary>
    void CompleteItem();

    /// <summary>
    /// Marks the queue as complete for writing.
    /// </summary>
    void Complete();
}

/// <summary>
/// Channel-backed processing queue with duplicate prevention.
/// </summary>
public class ProcessingQueue : IProcessingQueue
{
    private readonly Channel<string> _channel;
    private readonly ConcurrentDictionary<string, byte> _queued = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<ProcessingQueue> _logger;
    private int _activeCount;

    /// <inheritdoc />
    public int ActiveCount => _activeCount;

    /// <summary>
    /// Initializes a new instance of <see cref="ProcessingQueue"/>.
    /// </summary>
    public ProcessingQueue(ILogger<ProcessingQueue> logger)
    {
        _logger = logger;
        _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });
    }

    /// <inheritdoc />
    public bool TryEnqueue(string filePath)
    {
        if (!_queued.TryAdd(filePath, 0))
        {
            _logger.LogDebug("File already queued, skipping: {File}", filePath);
            return false;
        }

        if (_channel.Writer.TryWrite(filePath))
        {
            _logger.LogInformation("File queued for processing: {File}", filePath);
            return true;
        }

        _queued.TryRemove(filePath, out _);
        return false;
    }

    /// <inheritdoc />
    public async ValueTask<string> DequeueAsync(CancellationToken cancellationToken = default)
    {
        var filePath = await _channel.Reader.ReadAsync(cancellationToken);
        Interlocked.Increment(ref _activeCount);
        _queued.TryRemove(filePath, out _);
        return filePath;
    }

    /// <inheritdoc />
    public void CompleteItem()
    {
        Interlocked.Decrement(ref _activeCount);
    }

    /// <inheritdoc />
    public void Complete()
    {
        _channel.Writer.TryComplete();
    }
}
