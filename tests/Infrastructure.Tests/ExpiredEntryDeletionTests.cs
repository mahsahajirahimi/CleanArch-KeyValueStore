using KeyValueStore.Infrastructure.Storage.InMemory;
using KeyValueStore.Infrastructure.Storage.Persistent;
using Xunit;

namespace KeyValueStore.Infrastructure.Tests;

public sealed class ExpiredEntryDeletionTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"KvsExpiredDeletion-{Guid.NewGuid():N}");

    private string StorePath =>
        Path.Combine(_directory, "store.log");

    [Fact]
    public void InMemory_DeleteExpiredEntry_ShouldReturnFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new ManualTimeProvider(now);

        var repository =
            new InMemoryKeyValueEntryRepository(clock);

        repository.Upsert(
            "team-a",
            "temporary",
            "value",
            now,
            now.AddSeconds(10));

        Assert.NotNull(
            repository.Get("team-a", "temporary"));

        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.False(
            repository.Delete("team-a", "temporary"));

        Assert.Null(
            repository.Get("team-a", "temporary"));

        Assert.False(
            repository.Delete("team-a", "temporary"));
    }

    [Fact]
    public void Persistent_DeleteExpiredCachedEntry_ShouldNotAppendTombstone()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new ManualTimeProvider(now);

        var repository =
            new LogStructuredKeyValueEntryRepository(
                StorePath,
                timeProvider: clock);

        repository.Upsert(
            "team-a",
            "temporary",
            "value",
            now,
            now.AddSeconds(10));

        Assert.NotNull(
            repository.Get("team-a", "temporary"));

        long lengthBefore = new FileInfo(StorePath).Length;

        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.False(
            repository.Delete("team-a", "temporary"));

        Assert.Equal(
            lengthBefore,
            new FileInfo(StorePath).Length);

        Assert.Null(
            repository.Get("team-a", "temporary"));

        var restarted =
            new LogStructuredKeyValueEntryRepository(
                StorePath,
                timeProvider: clock);

        Assert.Null(
            restarted.Get("team-a", "temporary"));

        Assert.False(
            restarted.Delete("team-a", "temporary"));
    }

    [Fact]
    public void Persistent_DeleteExpiredUncachedEntry_ShouldNotAppendTombstone()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new ManualTimeProvider(now);

        var repository =
            new LogStructuredKeyValueEntryRepository(
                StorePath,
                cacheCapacity: 1,
                timeProvider: clock);

        repository.Upsert(
            "team-a",
            "temporary",
            "expires",
            now,
            now.AddSeconds(10));

        repository.Upsert(
            "team-a",
            "permanent",
            "stays",
            now);

        long lengthBefore = new FileInfo(StorePath).Length;

        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.False(
            repository.Delete("team-a", "temporary"));

        Assert.Equal(
            lengthBefore,
            new FileInfo(StorePath).Length);

        Assert.Null(
            repository.Get("team-a", "temporary"));

        Assert.Equal(
            "stays",
            repository.Get("team-a", "permanent")?.Value);

        var restarted =
            new LogStructuredKeyValueEntryRepository(
                StorePath,
                cacheCapacity: 1,
                timeProvider: clock);

        Assert.Null(
            restarted.Get("team-a", "temporary"));

        Assert.Equal(
            "stays",
            restarted.Get("team-a", "permanent")?.Value);
    }

    [Fact]
    public void Persistent_ExpiredLatestVersion_ShouldNotRevealOldValue()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new ManualTimeProvider(now);

        var repository =
            new LogStructuredKeyValueEntryRepository(
                StorePath,
                timeProvider: clock);

        repository.Upsert(
            "team-a",
            "name",
            "old-value",
            now,
            now.AddHours(1));

        repository.Upsert(
            "team-a",
            "name",
            "new-value",
            now.AddSeconds(1),
            now.AddSeconds(10));

        Assert.Equal(
            "new-value",
            repository.Get("team-a", "name")?.Value);

        long lengthBefore = new FileInfo(StorePath).Length;

        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.False(
            repository.Delete("team-a", "name"));

        Assert.Equal(
            lengthBefore,
            new FileInfo(StorePath).Length);

        var restarted =
            new LogStructuredKeyValueEntryRepository(
                StorePath,
                timeProvider: clock);

        Assert.Null(
            restarted.Get("team-a", "name"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(
                _directory,
                recursive: true);
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset initialTime)
        {
            _utcNow = initialTime;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }
}