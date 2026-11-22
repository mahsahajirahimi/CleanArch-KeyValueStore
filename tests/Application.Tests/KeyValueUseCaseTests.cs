using KeyValueStore.Application.Tests.Fakes;
using KeyValueStore.Application.UseCases;
using KeyValueStore.Domain.Aggregates;
using Xunit;

namespace KeyValueStore.Application.Tests;

public class KeyValueUseCaseTests
{
    [Fact]
    public void Set_ShouldPassValuesToRepository()
    {
        var repository = new FakeKeyValueEntryRepository();
        var useCase = new SetEntryUseCase(repository);
        var now = DateTimeOffset.UnixEpoch;

        useCase.Execute("userA", "name", "Ali", now);

        var call = repository.LastUpsert;

        Assert.True(call.HasValue);
        Assert.Equal("userA", call.Value.Keyspace);
        Assert.Equal("name", call.Value.Key);
        Assert.Equal("Ali", call.Value.Value);
        Assert.Equal(now, call.Value.Now);
        Assert.Null(call.Value.ExpiresAt);
    }

    [Fact]
    public void Set_ShouldCalculateExpirationFromTtl()
    {
        var repository = new FakeKeyValueEntryRepository();
        var useCase = new SetEntryUseCase(repository);
        var now = DateTimeOffset.UnixEpoch;

        useCase.Execute(
            "userA",
            "name",
            "Ali",
            now,
            ttlSeconds: 300);

        var call = repository.LastUpsert;

        Assert.True(call.HasValue);
        Assert.Equal(
            now.AddMinutes(5),
            call.Value.ExpiresAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Set_ShouldRejectInvalidTtl(int ttlSeconds)
    {
        var repository = new FakeKeyValueEntryRepository();
        var useCase = new SetEntryUseCase(repository);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            useCase.Execute(
                "userA",
                "name",
                "Ali",
                DateTimeOffset.UnixEpoch,
                ttlSeconds));

        Assert.Null(repository.LastUpsert);
    }

    [Fact]
    public void Set_ShouldRejectEmptyKeyspace()
    {
        var repository = new FakeKeyValueEntryRepository();
        var useCase = new SetEntryUseCase(repository);

        Assert.Throws<ArgumentException>(() =>
            useCase.Execute(
                "",
                "name",
                "Ali",
                DateTimeOffset.UnixEpoch));

        Assert.Null(repository.LastUpsert);
    }

    [Fact]
    public void Get_ShouldReturnExistingEntry()
    {
        var repository = new FakeKeyValueEntryRepository();
        var useCase = new GetEntryUseCase(repository);

        var entry = new KeyValueEntry(
            "name",
            "Ali",
            DateTimeOffset.UnixEpoch);

        repository.Save("userA", entry);

        var result = useCase.Execute("userA", "name");

        Assert.NotNull(result);
        Assert.Equal("Ali", result.Value);
    }

    [Fact]
    public void Get_ShouldReturnNullForMissingKey()
    {
        var repository = new FakeKeyValueEntryRepository();
        var useCase = new GetEntryUseCase(repository);

        var result = useCase.Execute("userA", "unknown");

        Assert.Null(result);
    }

    [Fact]
    public void Delete_ShouldRemoveExistingEntry()
    {
        var repository = new FakeKeyValueEntryRepository();
        var useCase = new DeleteEntryUseCase(repository);

        var entry = new KeyValueEntry(
            "name",
            "Ali",
            DateTimeOffset.UnixEpoch);

        repository.Save("userA", entry);

        bool deleted = useCase.Execute("userA", "name");

        Assert.True(deleted);
        Assert.Null(repository.Get("userA", "name"));
    }

    [Fact]
    public void Delete_ShouldReturnFalseForMissingKey()
    {
        var repository = new FakeKeyValueEntryRepository();
        var useCase = new DeleteEntryUseCase(repository);

        bool deleted = useCase.Execute("userA", "unknown");

        Assert.False(deleted);
    }
}