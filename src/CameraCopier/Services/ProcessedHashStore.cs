using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DanTheMan827.CameraCopier.Configuration;

namespace DanTheMan827.CameraCopier.Services;

/// <summary>
/// Represents the persisted state of processed file hashes.
/// </summary>
internal class ProcessedHashData
{
    [JsonPropertyName("processedHashes")]
    public HashSet<string> ProcessedHashes { get; set; } = [];
}

/// <summary>
/// Source-generated JSON serializer context for AOT-compatible serialization.
/// </summary>
[JsonSerializable(typeof(ProcessedHashData))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class ProcessedHashStoreContext : JsonSerializerContext { }

/// <summary>
/// Manages the set of file hashes that have already been processed.
/// </summary>
public interface IProcessedHashStore
{
    /// <summary>Loads hashes from disk into memory.</summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns true if the given hash has already been processed.</summary>
    bool Contains(string hash);

    /// <summary>Adds a hash to the store and persists it to disk.</summary>
    Task AddAsync(string hash, CancellationToken cancellationToken = default);
}

/// <summary>
/// JSON-backed store for tracking processed file hashes.
/// </summary>
public class ProcessedHashStore : IProcessedHashStore
{
    private readonly string _filePath;
    private readonly ILogger<ProcessedHashStore> _logger;
    private HashSet<string> _hashes = [];
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of <see cref="ProcessedHashStore"/>.
    /// </summary>
    public ProcessedHashStore(IOptions<AppSettings> options, ILogger<ProcessedHashStore> logger)
    {
        _filePath = options.Value.ProcessedHashFile;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            _logger.LogInformation("No processed hash file found at {Path}. Starting fresh.", _filePath);
            _hashes = [];
            return;
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var data = await JsonSerializer.DeserializeAsync(stream, ProcessedHashStoreContext.Default.ProcessedHashData, cancellationToken);
            _hashes = data?.ProcessedHashes ?? [];
            _logger.LogInformation("Loaded {Count} processed hashes from {Path}.", _hashes.Count, _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load processed hash file. Starting with empty set.");
            _hashes = [];
        }
    }

    /// <inheritdoc />
    public bool Contains(string hash) => _hashes.Contains(hash);

    /// <inheritdoc />
    public async Task AddAsync(string hash, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _hashes.Add(hash);
            await PersistAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        var data = new ProcessedHashData { ProcessedHashes = _hashes };
        var tempPath = _filePath + ".tmp";
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, data, ProcessedHashStoreContext.Default.ProcessedHashData, cancellationToken);
            }
            File.Move(tempPath, _filePath, overwrite: true);
            _logger.LogDebug("Persisted {Count} hashes to {Path}.", _hashes.Count, _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist processed hash file.");
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
