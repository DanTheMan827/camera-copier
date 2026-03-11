using System.Security.Cryptography;

namespace DanTheMan827.CameraCopier.Services;

/// <summary>
/// Computes SHA256 hashes for files.
/// </summary>
public interface IHashService
{
    /// <summary>
    /// Computes the SHA256 hash of the specified file asynchronously.
    /// </summary>
    /// <param name="filePath">The path to the file to hash.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The lowercase hex-encoded SHA256 hash string.</returns>
    Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides SHA256 hash computation for files.
/// </summary>
public class HashService : IHashService
{
    /// <inheritdoc />
    public async Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hashBytes);
    }
}
