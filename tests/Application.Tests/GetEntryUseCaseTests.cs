using KeyValueStore.Application.Tests.Fakes;
using KeyValueStore.Application.UseCases;
using KeyValueStore.Domain.Aggregates;
using KeyValueStore.Domain.Security;
using Xunit;

namespace KeyValueStore.Application.Tests;

public class GetEntryUseCaseTests
{
    [Fact]
    public void Get_ShouldReturnExistingEntry()
    {
        var (repository, useCase, requesterId) = CreateContext();

        var entry = new KeyValueEntry(
            "name",
            "Ali",
            DateTimeOffset.UnixEpoch);

        repository.Save("userA", entry);

        var result = useCase.Execute(
            requesterId,
            "userA",
            "name");

        Assert.NotNull(result);
        Assert.Equal("Ali", result.Value);
    }

    [Fact]
    public void Get_ShouldReturnNullForMissingKey()
    {
        var (_, useCase, requesterId) = CreateContext();

        var result = useCase.Execute(
            requesterId,
            "userA",
            "unknown");

        Assert.Null(result);
    }

    private static (
        FakeKeyValueEntryRepository Repository,
        GetEntryUseCase UseCase,
        Guid RequesterId
    ) CreateContext()
    {
        var repository = new FakeKeyValueEntryRepository();
        var users = new FakeUserAccountRepository();

        var reader = new UserAccount(
            Guid.NewGuid(),
            "userA",
            UserRole.Reader);

        users.Save(reader);

        var authorization =
            new AuthorizeEntryOperationUseCase(users);

        return (
            repository,
            new GetEntryUseCase(repository, authorization),
            reader.Id);
    }
}