using Microsoft.Extensions.Logging;

namespace DanTheMan827.CameraCopier.Services;

/// <summary>
/// Processes individual files: hashing, duplicate detection, metadata extraction, and copying.
/// </summary>
public interface IFileProcessor
{
    /// <summary>
    /// Processes a single file from the queue.
    /// </summary>
    /// <param name="filePath">The path of the file to process.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task ProcessAsync(string filePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Orchestrates the full processing pipeline for a single file.
/// </summary>
public class FileProcessor : IFileProcessor
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".heic", ".heif",
        ".mp4", ".mov"
    };

    private readonly IHashService _hashService;
    private readonly IProcessedHashStore _hashStore;
    private readonly IMetadataService _metadataService;
    private readonly ICopyService _copyService;
    private readonly ILogger<FileProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="FileProcessor"/>.
    /// </summary>
    public FileProcessor(
        IHashService hashService,
        IProcessedHashStore hashStore,
        IMetadataService metadataService,
        ICopyService copyService,
        ILogger<FileProcessor> logger)
    {
        _hashService = hashService;
        _hashStore = hashStore;
        _metadataService = metadataService;
        _copyService = copyService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ProcessAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(filePath);
        if (!SupportedExtensions.Contains(extension))
        {
            _logger.LogDebug("Unsupported file type, ignoring: {File}", filePath);
            return;
        }

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("File no longer exists: {File}", filePath);
            return;
        }

        _logger.LogInformation("Processing file: {File}", filePath);

        try
        {
            // Wait until file is readable (handles still-being-written files)
            await WaitForFileReadyAsync(filePath, cancellationToken);

            var hash = await _hashService.ComputeHashAsync(filePath, cancellationToken);

            if (_hashStore.Contains(hash))
            {
                _logger.LogInformation("File already processed (hash match), skipping: {File}", filePath);
                return;
            }

            var captureDate = _metadataService.GetCaptureDate(filePath);
            _logger.LogDebug("Capture date for {File}: {Date}", filePath, captureDate);

            var destPath = await _copyService.CopyAsync(filePath, captureDate, hash, cancellationToken);

            if (destPath != null)
            {
                await _hashStore.AddAsync(hash, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file: {File}", filePath);
        }
    }

    private static async Task WaitForFileReadyAsync(string filePath, CancellationToken cancellationToken, int maxAttempts = 10, int delayMs = 500)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                return;
            }
            catch (IOException)
            {
                if (attempt == maxAttempts - 1) throw;
                await Task.Delay(delayMs, cancellationToken);
            }
        }
    }
}
