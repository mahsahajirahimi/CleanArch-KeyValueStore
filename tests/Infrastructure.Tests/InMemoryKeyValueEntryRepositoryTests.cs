using KeyValueStore.Domain.Aggregates;
using KeyValueStore.Infrastructure.Storage.InMemory;
using Xunit;

namespace KeyValueStore.Infrastructure.Tests;

public class InMemoryKeyValueEntryRepositoryTests
{
    [Fact]
    public void Save_ShouldStoreEntry()
    {
        var repository = new InMemoryKeyValueEntryRepository();
        var now = DateTimeOffset.UnixEpoch;
        var entry = new KeyValueEntry("name", "Ali", now);

        repository.Save("userA", entry);

        var result = repository.Get("userA", "name");

        Assert.NotNull(result);
        Assert.Equal(entry.Key, result.Key);
        Assert.Equal(entry.Value, result.Value);
        Assert.NotSame(entry, result);
    }

    [Fact]
    public void Get_ShouldReturnNull_WhenKeyDoesNotExist()
    {
        var repository = new InMemoryKeyValueEntryRepository();

        var result = repository.Get("userA", "unknown");

        Assert.Null(result);
    }

    [Fact]
    public void Save_ShouldReplaceExistingEntry()
    {
        var repository = new InMemoryKeyValueEntryRepository();
        var now = DateTimeOffset.UnixEpoch;

        var first = new KeyValueEntry("name", "Ali", now);
        var second = new KeyValueEntry("name", "Sara", now);

        repository.Save("userA", first);
        repository.Save("userA", second);

        var result = repository.Get("userA", "name");

        Assert.NotNull(result);
        Assert.Equal("Sara", result.Value);
        Assert.NotSame(second, result);
    }

    [Fact]
    public void Get_ShouldIsolateKeyspaces()
    {
        var repository = new InMemoryKeyValueEntryRepository();
        var now = DateTimeOffset.UnixEpoch;

        var first = new KeyValueEntry("name", "Ali", now);
        var second = new KeyValueEntry("name", "Sara", now);

        repository.Save("userA", first);
        repository.Save("userB", second);

        var resultA = repository.Get("userA", "name");
        var resultB = repository.Get("userB", "name");

        Assert.NotNull(resultA);
        Assert.NotNull(resultB);

        Assert.Equal("Ali", resultA.Value);
        Assert.Equal("Sara", resultB.Value);
    }

    [Fact]
    public void Delete_ShouldRemoveEntry()
    {
        var repository = new InMemoryKeyValueEntryRepository();
        var entry = new KeyValueEntry(
            "name",
            "Ali",
            DateTimeOffset.UnixEpoch);

        repository.Save("userA", entry);

        bool deleted = repository.Delete("userA", "name");
        bool deletedAgain = repository.Delete("userA", "name");

        Assert.True(deleted);
        Assert.False(deletedAgain);
        Assert.Null(repository.Get("userA", "name"));
    }

    [Fact]
    public void Get_ShouldReturnNull_WhenEntryIsExpired()
    {
        var repository = new InMemoryKeyValueEntryRepository();
        var now = DateTimeOffset.UtcNow;

        var entry = new KeyValueEntry(
            "name",
            "Ali",
            now.AddMinutes(-10),
            now.AddMinutes(-5));

        repository.Save("userA", entry);

        var result = repository.Get("userA", "name");

        Assert.Null(result);

        Assert.False(repository.Delete("userA", "name"));
    }

    [Fact]
    public void Get_ShouldReturnEntry_WhenNotExpired()
    {
        var repository = new InMemoryKeyValueEntryRepository();
        var now = DateTimeOffset.UtcNow;

        var entry = new KeyValueEntry(
            "name",
            "Ali",
            now,
            now.AddHours(1));

        repository.Save("userA", entry);

        var result = repository.Get("userA", "name");

        Assert.NotNull(result);
        Assert.Equal("Ali", result.Value);
        Assert.NotSame(entry, result);
    }

    [Fact]
    public void Upsert_ShouldHandleConcurrentWrites()
    {
        var repository = new InMemoryKeyValueEntryRepository();
        var now = DateTimeOffset.UnixEpoch;

        Parallel.For(0, 500, i =>
        {
            repository.Upsert(
                "userA",
                $"key-{i}",
                i.ToString(),
                now);
        });

        for (int i = 0; i < 500; i++)
        {
            var entry = repository.Get("userA", $"key-{i}");

            Assert.NotNull(entry);
            Assert.Equal(i.ToString(), entry.Value);
        }
    }

    [Fact]
    public void Get_ShouldReturnIndependentCopy()
    {
        var repository = new InMemoryKeyValueEntryRepository();
        var now = DateTimeOffset.UnixEpoch;

        var original = new KeyValueEntry("name", "Ali", now);

        repository.Save("userA", original);

        var result = repository.Get("userA", "name");

        Assert.NotNull(result);

        result.UpdateValue("Sara", now.AddMinutes(1));

        var stored = repository.Get("userA", "name");

        Assert.NotNull(stored);
        Assert.Equal("Ali", stored.Value);
        Assert.NotSame(result, stored);
    }

    [Fact]
    public void Upsert_ShouldKeepTimestampMonotonic()
    {
        var repository = new InMemoryKeyValueEntryRepository();
        var now = DateTimeOffset.UnixEpoch;

        repository.Upsert(
            "userA",
            "name",
            "Initial",
            now);

        Parallel.For(0, 500, i =>
        {
            repository.Upsert(
                "userA",
                "name",
                i.ToString(),
                now.AddTicks(i + 1));
        });

        var result = repository.Get("userA", "name");

        Assert.NotNull(result);
        Assert.Equal(now, result.CreatedAt);
        Assert.Equal(now.AddTicks(500), result.UpdatedAt);
    }

    [Fact]
    public void Upsert_ShouldStoreExpiration()
    {
        var repository = new InMemoryKeyValueEntryRepository();
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(10);

        repository.Upsert(
            "userA",
            "name",
            "Ali",
            now,
            expiresAt);

        var result = repository.Get("userA", "name");

        Assert.NotNull(result);
        Assert.Equal(expiresAt, result.ExpiresAt);
        Assert.False(result.IsExpired(now.AddMinutes(9)));
        Assert.True(result.IsExpired(now.AddMinutes(10)));
    }

    [Fact]
    public void Upsert_ShouldReplaceExistingExpiration()
    {
        var repository = new InMemoryKeyValueEntryRepository();
        var now = DateTimeOffset.UtcNow;

        repository.Upsert(
            "userA",
            "name",
            "Ali",
            now,
            now.AddMinutes(10));

        repository.Upsert(
            "userA",
            "name",
            "Sara",
            now.AddMinutes(1),
            now.AddMinutes(30));

        var result = repository.Get("userA", "name");

        Assert.NotNull(result);
        Assert.Equal("Sara", result.Value);
        Assert.Equal(now.AddMinutes(30), result.ExpiresAt);
        Assert.Equal(now, result.CreatedAt);
    }

    [Fact]
    public void Upsert_ShouldClearExpirationWhenOmitted()
    {
        var repository = new InMemoryKeyValueEntryRepository();
        var now = DateTimeOffset.UtcNow;

        repository.Upsert(
            "userA",
            "name",
            "Ali",
            now,
            now.AddMinutes(10));

        repository.Upsert(
            "userA",
            "name",
            "Sara",
            now.AddMinutes(1));

        var result = repository.Get("userA", "name");

        Assert.NotNull(result);
        Assert.Equal("Sara", result.Value);
        Assert.Null(result.ExpiresAt);
    }

    [Fact]
    public void Upsert_ShouldRecreateExpiredEntry()
    {
        var repository = new InMemoryKeyValueEntryRepository();
        var now = DateTimeOffset.UtcNow;

        var expiredEntry = new KeyValueEntry(
            "name",
            "Old",
            now.AddMinutes(-10),
            now.AddMinutes(-5));

        repository.Save("userA", expiredEntry);

        repository.Upsert(
            "userA",
            "name",
            "New",
            now,
            now.AddMinutes(20));

        var result = repository.Get("userA", "name");

        Assert.NotNull(result);
        Assert.Equal("New", result.Value);
        Assert.Equal(now, result.CreatedAt);
        Assert.Equal(now.AddMinutes(20), result.ExpiresAt);
    }
}