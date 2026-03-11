using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using DanTheMan827.CameraCopier.Configuration;
using DanTheMan827.CameraCopier.Services;

namespace DanTheMan827.CameraCopier.Tests;

/// <summary>
/// Tests for <see cref="FileProcessor"/>.
/// </summary>
public class FileProcessorTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly Mock<IHashService> _hashService = new();
    private readonly Mock<IProcessedHashStore> _hashStore = new();
    private readonly Mock<IMetadataService> _metadataService = new();
    private readonly Mock<ICopyService> _copyService = new();

    public FileProcessorTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    private FileProcessor CreateProcessor() =>
        new(_hashService.Object, _hashStore.Object, _metadataService.Object,
            _copyService.Object, NullLogger<FileProcessor>.Instance);

    [Fact]
    public async Task ProcessAsync_UnsupportedExtension_DoesNotProcess()
    {
        var file = Path.Combine(_tempDir, "doc.txt");
        await File.WriteAllTextAsync(file, "text");

        var sut = CreateProcessor();
        await sut.ProcessAsync(file);

        _hashService.Verify(h => h.ComputeHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_MissingFile_DoesNotThrow()
    {
        var sut = CreateProcessor();
        await sut.ProcessAsync(Path.Combine(_tempDir, "nonexistent.jpg"));
        // No exception = pass
    }

    [Fact]
    public async Task ProcessAsync_AlreadyProcessedHash_SkipsFile()
    {
        var file = Path.Combine(_tempDir, "processed.jpg");
        await File.WriteAllTextAsync(file, "data");

        _hashService.Setup(h => h.ComputeHashAsync(file, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("knownhash");
        _hashStore.Setup(s => s.Contains("knownhash")).Returns(true);

        var sut = CreateProcessor();
        await sut.ProcessAsync(file);

        _copyService.Verify(c => c.CopyAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_NewFile_CopiesAndRecordsHash()
    {
        var file = Path.Combine(_tempDir, "new.jpg");
        await File.WriteAllTextAsync(file, "data");
        var captureDate = new DateTime(2025, 8, 14);

        _hashService.Setup(h => h.ComputeHashAsync(file, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("newhash");
        _hashStore.Setup(s => s.Contains("newhash")).Returns(false);
        _metadataService.Setup(m => m.GetCaptureDate(file)).Returns(captureDate);
        _copyService.Setup(c => c.CopyAsync(file, captureDate, "newhash", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Path.Combine(_tempDir, "dest", "new.jpg"));

        var sut = CreateProcessor();
        await sut.ProcessAsync(file);

        _hashStore.Verify(s => s.AddAsync("newhash", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_CopyReturnsNull_DoesNotRecordHash()
    {
        var file = Path.Combine(_tempDir, "dup.jpg");
        await File.WriteAllTextAsync(file, "data");
        var captureDate = new DateTime(2025, 5, 1);

        _hashService.Setup(h => h.ComputeHashAsync(file, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("duphash");
        _hashStore.Setup(s => s.Contains("duphash")).Returns(false);
        _metadataService.Setup(m => m.GetCaptureDate(file)).Returns(captureDate);
        _copyService.Setup(c => c.CopyAsync(file, captureDate, "duphash", It.IsAny<CancellationToken>()))
                    .ReturnsAsync((string?)null); // already at destination

        var sut = CreateProcessor();
        await sut.ProcessAsync(file);

        _hashStore.Verify(s => s.AddAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_CorruptedFileError_DoesNotThrow()
    {
        var file = Path.Combine(_tempDir, "corrupt.jpg");
        await File.WriteAllTextAsync(file, "corrupt");

        _hashService.Setup(h => h.ComputeHashAsync(file, It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new IOException("read error"));

        var sut = CreateProcessor();
        // Should log error and not rethrow
        await sut.ProcessAsync(file);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }
}
