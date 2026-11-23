
using KeyValueStore.Infrastructure.Storage.Persistent;
using Xunit;

namespace KeyValueStore.Infrastructure.Tests;

public sealed class PersistentKeyValueEntryRepositoryTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        $"KeyValueStorePersistentTests-{Guid.NewGuid():N}");

    private string StorePath =>
        Path.Combine(_testDirectory, "store.json");

    [Fact]
    public void Upsert_ShouldRestoreEntryAfterRestart()
    {
        var now = DateTimeOffset.UtcNow;
        var createdAt = now.AddMinutes(-10);
        var updatedAt = now.AddMinutes(-5);
        var expiresAt = now.AddHours(1);

        var repository = new PersistentKeyValueEntryRepository(StorePath);

        repository.Upsert(
            "userA",
            "name",
            "Ali",
            createdAt);

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

        // Simulate restarting the application.
        var restartedRepository =
            new PersistentKeyValueEntryRepository(StorePath);

        var result = restartedRepository.Get("userA", "name");

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
        var repository = new PersistentKeyValueEntryRepository(StorePath);

        repository.Upsert(
            "userA",
            "name",
            "Ali",
            DateTimeOffset.UtcNow);

        Assert.True(repository.Delete("userA", "name"));

        var restartedRepository =
            new PersistentKeyValueEntryRepository(StorePath);

        Assert.Null(restartedRepository.Get("userA", "name"));
    }

    [Fact]
    public void Repository_ShouldPreserveKeyspaceIsolationAfterRestart()
    {
        var repository = new PersistentKeyValueEntryRepository(StorePath);
        var now = DateTimeOffset.UtcNow;

        repository.Upsert("userA", "name", "Ali", now);
        repository.Upsert("userB", "name", "Sara", now);

        var restartedRepository =
            new PersistentKeyValueEntryRepository(StorePath);

        var first = restartedRepository.Get("userA", "name");
        var second = restartedRepository.Get("userB", "name");

        Assert.NotNull(first);
        Assert.NotNull(second);

        Assert.Equal("Ali", first.Value);
        Assert.Equal("Sara", second.Value);
    }

    [Fact]
    public void Get_ShouldHideExpiredEntryAfterRestart()
    {
        var now = DateTimeOffset.UtcNow;

        var repository = new PersistentKeyValueEntryRepository(StorePath);

        repository.Upsert(
            "userA",
            "name",
            "Ali",
            now.AddMinutes(-10),
            now.AddMinutes(-5));

        var restartedRepository =
            new PersistentKeyValueEntryRepository(StorePath);

        Assert.Null(restartedRepository.Get("userA", "name"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }
}