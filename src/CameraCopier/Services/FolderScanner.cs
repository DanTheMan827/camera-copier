using Microsoft.Extensions.Logging;

namespace DanTheMan827.CameraCopier.Services;

/// <summary>
/// Scans source folders recursively and enqueues files for processing.
/// </summary>
public interface IFolderScanner
{
    /// <summary>
    /// Scans the given folder recursively, enqueuing all discovered media files.
    /// </summary>
    /// <param name="folderPath">The folder to scan.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task ScanAsync(string folderPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Recursively enumerates files in a folder and pushes them into the processing queue.
/// </summary>
public class FolderScanner : IFolderScanner
{
    private readonly IProcessingQueue _queue;
    private readonly ILogger<FolderScanner> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="FolderScanner"/>.
    /// </summary>
    public FolderScanner(IProcessingQueue queue, ILogger<FolderScanner> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task ScanAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folderPath))
        {
            _logger.LogWarning("Source folder does not exist: {Folder}", folderPath);
            return Task.CompletedTask;
        }

        _logger.LogInformation("Scanning folder: {Folder}", folderPath);
        int count = 0;

        foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("File discovered: {File}", file);
            if (_queue.TryEnqueue(file))
            {
                count++;
            }
        }

        _logger.LogInformation("Scan complete for {Folder}: {Count} files enqueued.", folderPath, count);
        return Task.CompletedTask;
    }
}
