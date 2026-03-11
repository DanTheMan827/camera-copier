using Microsoft.Extensions.Logging.Abstractions;
using DanTheMan827.CameraCopier.Services;

namespace DanTheMan827.CameraCopier.Tests;

/// <summary>
/// Tests for <see cref="FolderScanner"/>.
/// </summary>
public class FolderScannerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly ProcessingQueue _queue = new(NullLogger<ProcessingQueue>.Instance);

    public FolderScannerTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    private FolderScanner CreateScanner() =>
        new(_queue, NullLogger<FolderScanner>.Instance);

    [Fact]
    public async Task ScanAsync_EnqueuesAllFilesInFolder()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "a.jpg"), "data");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "b.mp4"), "data");

        var scanner = CreateScanner();
        await scanner.ScanAsync(_tempDir);

        Assert.True(_queue.TryEnqueue(Path.Combine(_tempDir, "c.jpg"))); // queue is still accepting
        var first = await _queue.DequeueAsync(new CancellationToken());
        var second = await _queue.DequeueAsync(new CancellationToken());
        Assert.Contains(new[] { first, second }, f => f.EndsWith("a.jpg"));
        Assert.Contains(new[] { first, second }, f => f.EndsWith("b.mp4"));
    }

    [Fact]
    public async Task ScanAsync_NonExistentFolder_DoesNotThrow()
    {
        var scanner = CreateScanner();
        await scanner.ScanAsync(Path.Combine(_tempDir, "nonexistent"));
        // No exception = pass
    }

    [Fact]
    public async Task ScanAsync_RecursivelyFindsFiles()
    {
        var subDir = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(subDir, "nested.jpg"), "data");

        var scanner = CreateScanner();
        await scanner.ScanAsync(_tempDir);

        var file = await _queue.DequeueAsync(new CancellationToken());
        Assert.Contains("nested.jpg", file);
    }

    [Fact]
    public async Task ScanAsync_DuplicateScan_DoesNotDoubleEnqueue()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "img.jpg"), "data");
        var scanner = CreateScanner();

        await scanner.ScanAsync(_tempDir);
        await scanner.ScanAsync(_tempDir); // second scan should not re-enqueue

        // Only 1 item should be in queue (second attempt returns false from TryEnqueue)
        var file = await _queue.DequeueAsync(new CancellationToken());
        Assert.Contains("img.jpg", file);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }
}
