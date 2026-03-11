using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;
using Microsoft.Extensions.Logging;

namespace DanTheMan827.CameraCopier.Services;

/// <summary>
/// Extracts capture date metadata from image and video files.
/// </summary>
public interface IMetadataService
{
    /// <summary>
    /// Returns the capture date of the file, falling back to <see cref="FileInfo.CreationTime"/> if metadata is unavailable.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <returns>The capture <see cref="DateTime"/>.</returns>
    DateTime GetCaptureDate(string filePath);
}

/// <summary>
/// Reads EXIF / container metadata to determine the capture date of a media file.
/// </summary>
public class MetadataService : IMetadataService
{
    private readonly ILogger<MetadataService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="MetadataService"/>.
    /// </summary>
    public MetadataService(ILogger<MetadataService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public DateTime GetCaptureDate(string filePath)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);

            // Try EXIF DateTimeOriginal first (images)
            var exifSubIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (exifSubIfd != null &&
                exifSubIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var exifDate))
            {
                _logger.LogDebug("Extracted EXIF DateTimeOriginal {Date} from {File}.", exifDate, filePath);
                return exifDate;
            }

            // Try QuickTime movie header (videos)
            var qtMovie = directories.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();
            if (qtMovie != null &&
                qtMovie.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out var qtDate))
            {
                _logger.LogDebug("Extracted QuickTime creation date {Date} from {File}.", qtDate, filePath);
                return qtDate;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read metadata from {File}. Falling back to file creation time.", filePath);
        }

        // Fallback to file creation time
        var creationTime = new FileInfo(filePath).CreationTime;
        _logger.LogDebug("Using file creation time {Date} for {File}.", creationTime, filePath);
        return creationTime;
    }
}
