using KeyValueStore.Application.Tests.Fakes;
using KeyValueStore.Application.UseCases;
using KeyValueStore.Domain.Security;
using Xunit;

namespace KeyValueStore.Application.Tests;

public class SetEntryUseCaseTests
{
    [Fact]
    public void Set_ShouldPassValuesToRepository()
    {
        var (repository, useCase, requesterId) = CreateContext();
        var now = DateTimeOffset.UnixEpoch;

        useCase.Execute(
            requesterId,
            "userA",
            "name",
            "Ali",
            now);

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
        var (repository, useCase, requesterId) = CreateContext();
        var now = DateTimeOffset.UnixEpoch;

        useCase.Execute(
            requesterId,
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
        var (repository, useCase, requesterId) = CreateContext();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            useCase.Execute(
                requesterId,
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
        var (repository, useCase, requesterId) = CreateContext();

        Assert.Throws<ArgumentException>(() =>
            useCase.Execute(
                requesterId,
                "",
                "name",
                "Ali",
                DateTimeOffset.UnixEpoch));

        Assert.Null(repository.LastUpsert);
    }

    [Fact]
    public void Set_ShouldForwardMetadataToRepository()
    {
        var (repository, useCase, requesterId) = CreateContext();
        var now = DateTimeOffset.UnixEpoch;

        var metadata = new Dictionary<string, string>
        {
            ["department"] = "engineering",
            ["environment"] = "development"
        };

        useCase.Execute(
            requesterId,
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

    [Fact]
    public void Set_ShouldRejectNullValue()
    {
        var (repository, useCase, requesterId) = CreateContext();

        Assert.Throws<ArgumentException>(() =>
            useCase.Execute(
                requesterId,
                "userA",
                "name",
                null,
                DateTimeOffset.UnixEpoch));

        Assert.Null(repository.LastUpsert);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Set_ShouldRejectInvalidMetadataKeys(
        string metadataKey)
    {
        var (repository, useCase, requesterId) = CreateContext();

        var metadata = new Dictionary<string, string>
        {
            [metadataKey] = "value"
        };

        Assert.Throws<ArgumentException>(() =>
            useCase.Execute(
                requesterId,
                "userA",
                "name",
                "Ali",
                DateTimeOffset.UnixEpoch,
                metadata: metadata));

        Assert.Null(repository.LastUpsert);
    }

    [Fact]
    public void Set_ShouldRejectNullMetadataValue()
    {
        var (repository, useCase, requesterId) = CreateContext();

        var metadata = new Dictionary<string, string>
        {
            ["source"] = null!
        };

        Assert.Throws<ArgumentException>(() =>
            useCase.Execute(
                requesterId,
                "userA",
                "name",
                "Ali",
                DateTimeOffset.UnixEpoch,
                metadata: metadata));

        Assert.Null(repository.LastUpsert);
    }

    private static (
        FakeKeyValueEntryRepository Repository,
        SetEntryUseCase UseCase,
        Guid RequesterId
    ) CreateContext()
    {
        var repository = new FakeKeyValueEntryRepository();
        var users = new FakeUserAccountRepository();

        var writer = new UserAccount(
            Guid.NewGuid(),
            "userA",
            UserRole.Writer);

        users.Save(writer);

        var authorization =
            new AuthorizeEntryOperationUseCase(users);

        var useCase =
            new SetEntryUseCase(repository, authorization);

        return (repository, useCase, writer.Id);
    }
}