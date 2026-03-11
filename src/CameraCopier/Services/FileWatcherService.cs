using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DanTheMan827.CameraCopier.Configuration;

namespace DanTheMan827.CameraCopier.Services;

/// <summary>
/// Background service that:
/// <list type="bullet">
///   <item>Sets up <see cref="FileSystemWatcher"/> instances for each source folder.</item>
///   <item>Performs a startup scan of all source folders.</item>
///   <item>Runs periodic scans at the configured interval.</item>
///   <item>Spawns consumer tasks to process queued files.</item>
/// </list>
/// </summary>
public class FileWatcherService : BackgroundService
{
    private readonly AppSettings _settings;
    private readonly IProcessingQueue _queue;
    private readonly IFolderScanner _scanner;
    private readonly IFileProcessor _processor;
    private readonly IProcessedHashStore _hashStore;
    private readonly ILogger<FileWatcherService> _logger;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly SemaphoreSlim _scanLock = new(1, 1);

    // Temporary files that should be ignored
    private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tmp", ".part", ".crdownload", ".download"
    };

    /// <summary>
    /// Initializes a new instance of <see cref="FileWatcherService"/>.
    /// </summary>
    public FileWatcherService(
        IOptions<AppSettings> options,
        IProcessingQueue queue,
        IFolderScanner scanner,
        IFileProcessor processor,
        IProcessedHashStore hashStore,
        ILogger<FileWatcherService> logger)
    {
        _settings = options.Value;
        _queue = queue;
        _scanner = scanner;
        _processor = processor;
        _hashStore = hashStore;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _hashStore.LoadAsync(stoppingToken);

        // Start consumer workers
        var consumerTasks = Enumerable
            .Range(0, _settings.MaxConcurrentProcessors)
            .Select(_ => Task.Run(() => RunConsumerAsync(stoppingToken), stoppingToken))
            .ToArray();

        // Set up watchers
        foreach (var folder in _settings.SourceFolders)
        {
            SetupWatcher(folder, stoppingToken);
        }

        // Startup scan
        await RunScanAsync(stoppingToken);

        // Periodic scan loop
        var scanInterval = TimeSpan.FromSeconds(_settings.ScanIntervalSeconds);
        using var timer = new PeriodicTimer(scanInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunScanAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        finally
        {
            _queue.Complete();
            await Task.WhenAll(consumerTasks);
            DisposeWatchers();
        }
    }

    private void SetupWatcher(string folder, CancellationToken stoppingToken)
    {
        if (!Directory.Exists(folder))
        {
            _logger.LogWarning("Source folder does not exist, watcher not created: {Folder}", folder);
            return;
        }

        var watcher = new FileSystemWatcher(folder)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        watcher.Created += (_, e) => OnFileEvent(e.FullPath, stoppingToken);
        watcher.Renamed += (_, e) => OnFileEvent(e.FullPath, stoppingToken);
        watcher.Error += (_, e) => _logger.LogError(e.GetException(), "FileSystemWatcher error on {Folder}", folder);

        _watchers.Add(watcher);
        _logger.LogInformation("Watching folder: {Folder}", folder);
    }

    private void OnFileEvent(string fullPath, CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested) return;

        var ext = Path.GetExtension(fullPath);
        if (IgnoredExtensions.Contains(ext)) return;

        _logger.LogDebug("FileSystemWatcher event: {File}", fullPath);
        _queue.TryEnqueue(fullPath);
    }

    private async Task RunScanAsync(CancellationToken cancellationToken)
    {
        if (_queue.ActiveCount > 0)
        {
            _logger.LogInformation("Skipping periodic scan — {Count} files still being processed.", _queue.ActiveCount);
            return;
        }

        if (!await _scanLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogInformation("Scan already in progress, skipping.");
            return;
        }

        try
        {
            _logger.LogInformation("Starting folder scan.");
            foreach (var folder in _settings.SourceFolders)
            {
                await _scanner.ScanAsync(folder, cancellationToken);
            }
        }
        finally
        {
            _scanLock.Release();
        }
    }

    private async Task RunConsumerAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                var filePath = await _queue.DequeueAsync(cancellationToken);
                try
                {
                    await _processor.ProcessAsync(filePath, cancellationToken);
                }
                finally
                {
                    _queue.CompleteItem();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Consumer task failed unexpectedly.");
        }
    }

    private void DisposeWatchers()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }
        _watchers.Clear();
    }
}
