using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using DanTheMan827.CameraCopier.Configuration;
using DanTheMan827.CameraCopier.Services;

namespace DanTheMan827.CameraCopier.Tests;

/// <summary>
/// Tests for <see cref="CopyService"/>.
/// </summary>
public class CopyServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly Mock<IHashService> _hashServiceMock = new();

    public CopyServiceTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    private CopyService CreateService(string destSubDir = "dest")
    {
        var destDir = Path.Combine(_tempDir, destSubDir);
        var options = Options.Create(new AppSettings { DestinationFolder = destDir });
        return new CopyService(options, _hashServiceMock.Object, NullLogger<CopyService>.Instance);
    }

    [Fact]
    public async Task CopyAsync_NewFile_CopiesSuccessfully()
    {
        var srcFile = Path.Combine(_tempDir, "IMG_001.jpg");
        await File.WriteAllTextAsync(srcFile, "photo data");

        var sut = CreateService();
        var date = new DateTime(2025, 8, 14);
        var hash = "aabbcc";

        var result = await sut.CopyAsync(srcFile, date, hash);

        Assert.NotNull(result);
        Assert.True(File.Exists(result));
        Assert.Contains("2025-08-14", result);
        Assert.Contains("IMG_001.jpg", result);
    }

    [Fact]
    public async Task CopyAsync_CreatesDestinationDirectory()
    {
        var srcFile = Path.Combine(_tempDir, "IMG_002.jpg");
        await File.WriteAllTextAsync(srcFile, "data");

        var destDir = Path.Combine(_tempDir, "auto_dest");
        var options = Options.Create(new AppSettings { DestinationFolder = destDir });
        var sut = new CopyService(options, _hashServiceMock.Object, NullLogger<CopyService>.Instance);

        var result = await sut.CopyAsync(srcFile, new DateTime(2025, 1, 1), "hash1");

        Assert.NotNull(result);
        Assert.True(File.Exists(result));
    }

    [Fact]
    public async Task CopyAsync_SameNameSameHash_ReturnsNull()
    {
        var srcFile = Path.Combine(_tempDir, "IMG_003.jpg");
        await File.WriteAllTextAsync(srcFile, "data");

        var sut = CreateService("dup_dest");
        var date = new DateTime(2025, 6, 15);
        const string hash = "samehash";

        // First copy
        await sut.CopyAsync(srcFile, date, hash);

        // Mock hash service to return same hash for existing file
        _hashServiceMock
            .Setup(h => h.ComputeHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hash);

        // Second copy - same name, same hash -> should be skipped
        var result = await sut.CopyAsync(srcFile, date, hash);

        Assert.Null(result);
    }

    [Fact]
    public async Task CopyAsync_SameNameDifferentHash_AddsNumericSuffix()
    {
        var srcFile = Path.Combine(_tempDir, "IMG_004.jpg");
        await File.WriteAllTextAsync(srcFile, "original data");

        var sut = CreateService("suffix_dest");
        var date = new DateTime(2025, 3, 20);

        // First copy
        await sut.CopyAsync(srcFile, date, "hash_orig");

        // Mock hash service to return different hash for existing destination
        _hashServiceMock
            .Setup(h => h.ComputeHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("different_hash");

        // Second copy - same name, different hash -> should add suffix
        var result = await sut.CopyAsync(srcFile, date, "hash_new");

        Assert.NotNull(result);
        Assert.Contains("IMG_004 (1).jpg", result);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }
}
