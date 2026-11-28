using KeyValueStore.Application.Tests.Fakes;
using KeyValueStore.Application.UseCases;
using KeyValueStore.Domain.Aggregates;
using KeyValueStore.Domain.Security;
using Xunit;

namespace KeyValueStore.Application.Tests;

public class DeleteEntryUseCaseTests
{
    [Fact]
    public void Delete_ShouldRemoveExistingEntry()
    {
        var (repository, useCase, requesterId) = CreateContext();

        var entry = new KeyValueEntry(
            "name",
            "Ali",
            DateTimeOffset.UnixEpoch);

        repository.Save("userA", entry);

        bool deleted = useCase.Execute(
            requesterId,
            "userA",
            "name");

        Assert.True(deleted);
        Assert.Null(repository.Get("userA", "name"));
    }

    [Fact]
    public void Delete_ShouldReturnFalseForMissingKey()
    {
        var (_, useCase, requesterId) = CreateContext();

        bool deleted = useCase.Execute(
            requesterId,
            "userA",
            "unknown");

        Assert.False(deleted);
    }

    private static (
        FakeKeyValueEntryRepository Repository,
        DeleteEntryUseCase UseCase,
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

        return (
            repository,
            new DeleteEntryUseCase(repository, authorization),
            writer.Id);
    }
}