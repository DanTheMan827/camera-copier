using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using DanTheMan827.CameraCopier.Configuration;
using DanTheMan827.CameraCopier.Services;

namespace DanTheMan827.CameraCopier.Tests;

/// <summary>
/// Tests for <see cref="ProcessedHashStore"/>.
/// </summary>
public class ProcessedHashStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public ProcessedHashStoreTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    private ProcessedHashStore CreateStore(string fileName = "hashes.json")
    {
        var options = Options.Create(new AppSettings
        {
            ProcessedHashFile = Path.Combine(_tempDir, fileName)
        });
        return new ProcessedHashStore(options, NullLogger<ProcessedHashStore>.Instance);
    }

    [Fact]
    public async Task LoadAsync_MissingFile_StartsEmpty()
    {
        var store = CreateStore("missing.json");
        await store.LoadAsync();
        Assert.False(store.Contains("anyhash"));
    }

    [Fact]
    public async Task AddAsync_HashIsPersisted()
    {
        var store = CreateStore();
        await store.LoadAsync();
        await store.AddAsync("abc123");
        Assert.True(store.Contains("abc123"));
    }

    [Fact]
    public async Task AddAsync_PersistedToDisk_ReloadedCorrectly()
    {
        var fileName = "reload.json";
        var store1 = CreateStore(fileName);
        await store1.LoadAsync();
        await store1.AddAsync("deadbeef");

        var store2 = CreateStore(fileName);
        await store2.LoadAsync();
        Assert.True(store2.Contains("deadbeef"));
    }

    [Fact]
    public async Task Contains_ReturnsFalse_ForUnknownHash()
    {
        var store = CreateStore();
        await store.LoadAsync();
        Assert.False(store.Contains("unknown"));
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }
}
