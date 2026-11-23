using KeyValueStore.Domain.Aggregates;
using KeyValueStore.Infrastructure.Storage.Persistent;
using Xunit;

namespace KeyValueStore.Infrastructure.Tests;

public sealed class LogStructuredKeyValueEntryRepositoryTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        $"KeyValueStoreIndexedTests-{Guid.NewGuid():N}");

    private string StorePath =>
        Path.Combine(_testDirectory, "store.log");

    [Fact]
    public void Upsert_ShouldRestoreLatestValueAfterRestart()
    {
        var now = DateTimeOffset.UtcNow;
        var createdAt = now.AddMinutes(-10);
        var updatedAt = now.AddMinutes(-5);
        var expiresAt = now.AddHours(1);

        var repository =
            new LogStructuredKeyValueEntryRepository(StorePath);

        repository.Upsert("userA", "name", "Ali", createdAt);

        repository.Upsert(
            "userA",
            "name",
            "Sara",
            updatedAt,
            expiresAt,
            new Dictionary<string, string>
            {
                ["department"] = "engineering"
            });

        var restarted =
            new LogStructuredKeyValueEntryRepository(StorePath);

        var result = restarted.Get("userA", "name");

        Assert.NotNull(result);
        Assert.Equal("Sara", result.Value);
        Assert.Equal(createdAt, result.CreatedAt);
        Assert.Equal(updatedAt, result.UpdatedAt);
        Assert.Equal(expiresAt, result.ExpiresAt);
        Assert.Equal(
            "engineering",
            result.Metadata["department"]);
    }

    [Fact]
    public void Delete_ShouldRemainDeletedAfterRestart()
    {
        var repository =
            new LogStructuredKeyValueEntryRepository(StorePath);

        repository.Upsert(
            "userA",
            "name",
            "Ali",
            DateTimeOffset.UtcNow);

        Assert.True(repository.Delete("userA", "name"));

        var restarted =
            new LogStructuredKeyValueEntryRepository(StorePath);

        Assert.Null(restarted.Get("userA", "name"));
        Assert.False(restarted.Delete("userA", "name"));
    }

    [Fact]
    public void Repository_ShouldPreserveKeyspaceIsolation()
    {
        var repository =
            new LogStructuredKeyValueEntryRepository(StorePath);

        var now = DateTimeOffset.UtcNow;

        repository.Upsert("userA", "name", "Ali", now);
        repository.Upsert("userB", "name", "Sara", now);

        var restarted =
            new LogStructuredKeyValueEntryRepository(StorePath);

        Assert.Equal(
            "Ali",
            restarted.Get("userA", "name")?.Value);

        Assert.Equal(
            "Sara",
            restarted.Get("userB", "name")?.Value);
    }

    [Fact]
    public void ExpiredUpdate_ShouldNotRestoreOlderValue()
    {
        var now = DateTimeOffset.UtcNow;

        var repository =
            new LogStructuredKeyValueEntryRepository(StorePath);

        repository.Upsert(
            "userA",
            "name",
            "old",
            now.AddMinutes(-10),
            now.AddHours(1));

        repository.Upsert(
            "userA",
            "name",
            "expired",
            now.AddMinutes(-5),
            now.AddMinutes(-1));

        var restarted =
            new LogStructuredKeyValueEntryRepository(StorePath);

        Assert.Null(restarted.Get("userA", "name"));
    }

    [Fact]
    public void Save_ShouldPersistEntryAfterRestart()
    {
        var now = DateTimeOffset.UtcNow;

        var repository =
            new LogStructuredKeyValueEntryRepository(StorePath);

        repository.Save(
            "userA",
            new KeyValueEntry("city", "Tehran", now));

        var restarted =
            new LogStructuredKeyValueEntryRepository(StorePath);

        Assert.Equal(
            "Tehran",
            restarted.Get("userA", "city")?.Value);
    }

    [Fact]
    public void Startup_ShouldRecoverIncompleteTail()
    {
        var repository =
            new LogStructuredKeyValueEntryRepository(StorePath);

        repository.Upsert(
            "userA",
            "name",
            "Ali",
            DateTimeOffset.UtcNow);

        using (var stream = new FileStream(
            StorePath,
            FileMode.Append,
            FileAccess.Write))
        {
            stream.WriteByte(0x01);
        }

        var restarted =
            new LogStructuredKeyValueEntryRepository(StorePath);

        Assert.Equal(
            "Ali",
            restarted.Get("userA", "name")?.Value);

        Assert.Single(Directory.GetFiles(
            _testDirectory, "*.tail"));
    }

    [Fact]
    public void Restart_ShouldRestoreCorrectStateAfterManyOperations()
    {
        var repository =
            new LogStructuredKeyValueEntryRepository(StorePath);

        var now = DateTimeOffset.UtcNow;

        for (int i = 0; i < 100; i++)
        {
            repository.Upsert(
                "userA",
                $"key-{i:D3}",
                $"value-{i}",
                now);
        }

        for (int i = 0; i < 100; i += 2)
        {
            repository.Upsert(
                "userA",
                $"key-{i:D3}",
                $"updated-{i}",
                now.AddSeconds(1));
        }

        for (int i = 0; i < 100; i += 3)
        {
            Assert.True(repository.Delete(
                "userA",
                $"key-{i:D3}"));
        }

        var restarted =
            new LogStructuredKeyValueEntryRepository(StorePath);

        for (int i = 0; i < 100; i++)
        {
            var entry = restarted.Get(
                "userA",
                $"key-{i:D3}");

            if (i % 3 == 0)
            {
                Assert.Null(entry);
            }
            else
            {
                Assert.NotNull(entry);

                string expected = i % 2 == 0
                    ? $"updated-{i}"
                    : $"value-{i}";

                Assert.Equal(expected, entry.Value);
            }
        }
    }

    [Fact]
    public void Startup_ShouldRejectCorruptedCompleteRecord()
    {
        var repository =
            new LogStructuredKeyValueEntryRepository(StorePath);

        repository.Upsert(
            "userA",
            "name",
            "Ali",
            DateTimeOffset.UtcNow);

        long originalLength = new FileInfo(StorePath).Length;

        using (var stream = new FileStream(
            StorePath,
            FileMode.Open,
            FileAccess.Write))
        {
            stream.Position = sizeof(int);
            stream.WriteByte((byte)'!');
        }

        Assert.Throws<InvalidDataException>(() =>
            new LogStructuredKeyValueEntryRepository(StorePath));

        Assert.Equal(
            originalLength,
            new FileInfo(StorePath).Length);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }
}
