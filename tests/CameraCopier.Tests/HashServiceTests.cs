using Microsoft.Extensions.Logging.Abstractions;
using DanTheMan827.CameraCopier.Services;

namespace DanTheMan827.CameraCopier.Tests;

/// <summary>
/// Tests for <see cref="HashService"/>.
/// </summary>
public class HashServiceTests : IDisposable
{
    private readonly HashService _sut = new();
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public HashServiceTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task ComputeHashAsync_SameContent_ReturnsSameHash()
    {
        var file1 = Path.Combine(_tempDir, "a.jpg");
        var file2 = Path.Combine(_tempDir, "b.jpg");
        var content = "test content"u8.ToArray();
        await File.WriteAllBytesAsync(file1, content);
        await File.WriteAllBytesAsync(file2, content);

        var hash1 = await _sut.ComputeHashAsync(file1);
        var hash2 = await _sut.ComputeHashAsync(file2);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public async Task ComputeHashAsync_DifferentContent_ReturnsDifferentHash()
    {
        var file1 = Path.Combine(_tempDir, "c.jpg");
        var file2 = Path.Combine(_tempDir, "d.jpg");
        await File.WriteAllBytesAsync(file1, "content A"u8.ToArray());
        await File.WriteAllBytesAsync(file2, "content B"u8.ToArray());

        var hash1 = await _sut.ComputeHashAsync(file1);
        var hash2 = await _sut.ComputeHashAsync(file2);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public async Task ComputeHashAsync_ReturnsLowercaseHex()
    {
        var file = Path.Combine(_tempDir, "e.jpg");
        await File.WriteAllTextAsync(file, "data");

        var hash = await _sut.ComputeHashAsync(file);

        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }
}
