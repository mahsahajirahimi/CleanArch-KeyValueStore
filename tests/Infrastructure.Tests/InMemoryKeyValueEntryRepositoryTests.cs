using KeyValueStore.Domain.Aggregates;
using KeyValueStore.Infrastructure.Storage.InMemory;

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

        Assert.Same(entry, result);
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

        Assert.Same(second, result);
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

        Assert.Same(first, repository.Get("userA", "name"));
        Assert.Same(second, repository.Get("userB", "name"));
    }

    [Fact]
    public void Delete_ShouldRemoveEntry()
    {
        var repository = new InMemoryKeyValueEntryRepository();
        var entry = new KeyValueEntry(
            "name", "Ali", DateTimeOffset.UnixEpoch);

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

        Assert.Null(repository.Get("userA", "name"));

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

        Assert.Same(entry, repository.Get("userA", "name"));
    }
}