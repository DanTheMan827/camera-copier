namespace DanTheMan827.CameraCopier.Configuration;

/// <summary>
/// Application configuration settings loaded from appsettings.json.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Gets or sets the list of source folders to monitor for new photos and videos.
    /// </summary>
    public List<string> SourceFolders { get; set; } = [];

    /// <summary>
    /// Gets or sets the root destination folder where files will be organized by date.
    /// </summary>
    public string DestinationFolder { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to the JSON file storing processed file hashes.
    /// </summary>
    public string ProcessedHashFile { get; set; } = "processed.json";

    /// <summary>
    /// Gets or sets the interval in seconds between periodic full scans.
    /// </summary>
    public int ScanIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the maximum number of files to process concurrently.
    /// </summary>
    public int MaxConcurrentProcessors { get; set; } = 2;
}
