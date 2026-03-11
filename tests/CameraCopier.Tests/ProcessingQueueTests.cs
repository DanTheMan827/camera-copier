using Microsoft.Extensions.Logging.Abstractions;
using DanTheMan827.CameraCopier.Services;

namespace DanTheMan827.CameraCopier.Tests;

/// <summary>
/// Tests for <see cref="ProcessingQueue"/>.
/// </summary>
public class ProcessingQueueTests
{
    private ProcessingQueue CreateQueue() =>
        new(NullLogger<ProcessingQueue>.Instance);

    [Fact]
    public void TryEnqueue_NewFile_ReturnsTrue()
    {
        var queue = CreateQueue();
        Assert.True(queue.TryEnqueue("/some/file.jpg"));
    }

    [Fact]
    public void TryEnqueue_DuplicateFile_ReturnsFalse()
    {
        var queue = CreateQueue();
        queue.TryEnqueue("/some/file.jpg");
        Assert.False(queue.TryEnqueue("/some/file.jpg"));
    }

    [Fact]
    public async Task DequeueAsync_ReturnsEnqueuedFile()
    {
        var queue = CreateQueue();
        queue.TryEnqueue("/photos/img.jpg");
        var result = await queue.DequeueAsync();
        Assert.Equal("/photos/img.jpg", result);
    }

    [Fact]
    public async Task DequeueAsync_IncrementsActiveCount()
    {
        var queue = CreateQueue();
        queue.TryEnqueue("/file.jpg");
        Assert.Equal(0, queue.ActiveCount);
        _ = await queue.DequeueAsync();
        Assert.Equal(1, queue.ActiveCount);
    }

    [Fact]
    public async Task CompleteItem_DecrementsActiveCount()
    {
        var queue = CreateQueue();
        queue.TryEnqueue("/file.jpg");
        _ = await queue.DequeueAsync();
        queue.CompleteItem();
        Assert.Equal(0, queue.ActiveCount);
    }

    [Fact]
    public async Task AfterDequeue_SameFileCanBeReenqueued()
    {
        var queue = CreateQueue();
        queue.TryEnqueue("/file.jpg");
        _ = await queue.DequeueAsync();
        // After dequeue, the path is removed from _queued so re-enqueue should succeed
        Assert.True(queue.TryEnqueue("/file.jpg"));
    }

    [Fact]
    public void TryEnqueue_MultipleDistinctFiles_AllEnqueued()
    {
        var queue = CreateQueue();
        Assert.True(queue.TryEnqueue("/a.jpg"));
        Assert.True(queue.TryEnqueue("/b.jpg"));
        Assert.True(queue.TryEnqueue("/c.jpg"));
    }
}
