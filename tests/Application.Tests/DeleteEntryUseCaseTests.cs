
using KeyValueStore.Application.Tests.Fakes;
using KeyValueStore.Application.UseCases;
using KeyValueStore.Domain.Aggregates;
using Xunit;

namespace KeyValueStore.Application.Tests;

public class DeleteEntryUseCaseTests
{
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