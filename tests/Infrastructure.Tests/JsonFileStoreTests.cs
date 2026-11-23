
using System.Text.Json;
using KeyValueStore.Infrastructure.Storage.Persistent;
using Xunit;

namespace KeyValueStore.Infrastructure.Tests;

public sealed class JsonFileStoreTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        $"KeyValueStoreTests-{Guid.NewGuid():N}");

    private string StorePath =>
        Path.Combine(_testDirectory, "nested", "store.json");

    [Fact]
    public void Load_ShouldReturnEmpty_WhenFileDoesNotExist()
    {
        var store = new JsonFileStore(StorePath);

        var result = store.Load();

        Assert.Empty(result);
    }

    [Fact]
    public void SaveThenLoad_ShouldRestoreAllFields()
    {
        var store = new JsonFileStore(StorePath);
        var now = DateTimeOffset.UnixEpoch;

        var entries = new List<StoredEntry>
        {
            new(
                "userA",
                "name",
                "Ali",
                now,
                now.AddMinutes(2),
                now.AddMinutes(10),
                new Dictionary<string, string>
                {
                    ["department"] = "engineering"
                })
        };

        store.Save(entries);

        // Simulate loading data with a new store instance.
        var restoredStore = new JsonFileStore(StorePath);
        var restoredEntries = restoredStore.Load();

        var entry = Assert.Single(restoredEntries);

        Assert.Equal("userA", entry.Keyspace);
        Assert.Equal("name", entry.Key);
        Assert.Equal("Ali", entry.Value);
        Assert.Equal(now, entry.CreatedAt);
        Assert.Equal(now.AddMinutes(2), entry.UpdatedAt);
        Assert.Equal(now.AddMinutes(10), entry.ExpiresAt);
        Assert.Equal(
            "engineering",
            entry.Metadata["department"]);

        Assert.True(File.Exists(StorePath));
    }

    [Fact]
    public void Save_ShouldReplacePreviousSnapshot()
    {
        var store = new JsonFileStore(StorePath);
        var now = DateTimeOffset.UnixEpoch;

        store.Save(new List<StoredEntry>
        {
            new(
                "userA",
                "name",
                "Ali",
                now,
                now,
                null,
                new Dictionary<string, string>())
        });

        store.Save(new List<StoredEntry>
        {
            new(
                "userA",
                "city",
                "Tehran",
                now,
                now,
                null,
                new Dictionary<string, string>())
        });

        var restored = new JsonFileStore(StorePath).Load();

        var entry = Assert.Single(restored);

        Assert.Equal("city", entry.Key);
        Assert.Equal("Tehran", entry.Value);

        string directory = Path.GetDirectoryName(StorePath)!;

        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public void Load_ShouldThrow_WhenJsonIsInvalid()
    {
        string directory = Path.GetDirectoryName(StorePath)!;
        Directory.CreateDirectory(directory);

        File.WriteAllText(StorePath, "{ invalid json");

        var store = new JsonFileStore(StorePath);

        Assert.Throws<JsonException>(() => store.Load());
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }
}