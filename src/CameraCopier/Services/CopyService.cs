using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DanTheMan827.CameraCopier.Configuration;

namespace DanTheMan827.CameraCopier.Services;

/// <summary>
/// Copies source files to the structured destination directory.
/// </summary>
public interface ICopyService
{
    /// <summary>
    /// Copies the source file to the destination folder, handling name conflicts.
    /// </summary>
    /// <param name="sourcePath">Full path of the source file.</param>
    /// <param name="captureDate">Date used to determine the destination subfolder.</param>
    /// <param name="sourceHash">SHA256 hash of the source file for conflict resolution.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The destination path where the file was copied, or null if skipped.</returns>
    Task<string?> CopyAsync(string sourcePath, DateTime captureDate, string sourceHash, CancellationToken cancellationToken = default);
}

/// <summary>
/// Copies media files into a date-structured destination hierarchy.
/// </summary>
public class CopyService : ICopyService
{
    private readonly string _destinationRoot;
    private readonly IHashService _hashService;
    private readonly ILogger<CopyService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="CopyService"/>.
    /// </summary>
    public CopyService(IOptions<AppSettings> options, IHashService hashService, ILogger<CopyService> logger)
    {
        _destinationRoot = options.Value.DestinationFolder;
        _hashService = hashService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> CopyAsync(string sourcePath, DateTime captureDate, string sourceHash, CancellationToken cancellationToken = default)
    {
        var dateFolder = captureDate.ToString("yyyy-MM-dd");
        var destDir = Path.Combine(_destinationRoot, dateFolder);
        Directory.CreateDirectory(destDir);

        var fileName = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = Path.GetExtension(sourcePath);
        var destPath = Path.Combine(destDir, Path.GetFileName(sourcePath));

        // Resolve name conflicts
        int suffix = 1;
        while (File.Exists(destPath))
        {
            var existingHash = await _hashService.ComputeHashAsync(destPath, cancellationToken);
            if (existingHash.Equals(sourceHash, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Duplicate detected at destination, skipping: {Dest}", destPath);
                return null;
            }

            _logger.LogDebug("Name conflict at {Dest}, trying suffix {Suffix}.", destPath, suffix);
            destPath = Path.Combine(destDir, $"{fileName} ({suffix}){extension}");
            suffix++;
        }

        File.Copy(sourcePath, destPath);
        _logger.LogInformation("Copied {Source} → {Dest}", sourcePath, destPath);
        return destPath;
    }
}
