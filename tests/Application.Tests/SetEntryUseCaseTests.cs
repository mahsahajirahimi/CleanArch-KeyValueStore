
using KeyValueStore.Application.Tests.Fakes;
using KeyValueStore.Application.UseCases;
using Xunit;

namespace KeyValueStore.Application.Tests;

public class SetEntryUseCaseTests
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
        Assert.Null(call.Value.Metadata);
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
    public void Set_ShouldForwardMetadataToRepository()
    {
        var repository = new FakeKeyValueEntryRepository();
        var useCase = new SetEntryUseCase(repository);
        var now = DateTimeOffset.UnixEpoch;

        var metadata = new Dictionary<string, string>
        {
            ["department"] = "engineering",
            ["environment"] = "development"
        };

        useCase.Execute(
            "userA",
            "name",
            "Ali",
            now,
            metadata: metadata);

        var call = repository.LastUpsert;

        Assert.True(call.HasValue);
        Assert.Equal("userA", call.Value.Keyspace);
        Assert.Equal("name", call.Value.Key);
        Assert.Equal("Ali", call.Value.Value);

        var passedMetadata = call.Value.Metadata;

        Assert.NotNull(passedMetadata);
        Assert.Equal(2, passedMetadata.Count);
        Assert.Equal(
            "engineering",
            passedMetadata["department"]);
        Assert.Equal(
            "development",
            passedMetadata["environment"]);
    }
}