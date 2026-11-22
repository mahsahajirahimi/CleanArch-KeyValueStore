using KeyValueStore.Application.Tests.Fakes;
using KeyValueStore.Application.UseCases;
using Xunit;

namespace KeyValueStore.Application.Tests;

public class KeyValueUseCaseTests
{
    [Fact]
    public void Set_ShouldCreateNewEntry()
    {
        var repository = new FakeKeyValueEntryRepository();
        var useCase = new SetEntryUseCase(repository);
        var now = DateTimeOffset.UnixEpoch;

        useCase.Execute("userA", "name", "Ali", now);

        var entry = repository.Get("userA", "name");

        Assert.NotNull(entry);
        Assert.Equal("Ali", entry.Value);
        Assert.Equal(now, entry.CreatedAt);
    }

    [Fact]
    public void Set_ShouldUpdateExistingEntry()
    {
        var repository = new FakeKeyValueEntryRepository();
        var useCase = new SetEntryUseCase(repository);
        var now = DateTimeOffset.UnixEpoch;

        useCase.Execute("userA", "name", "Ali", now);
        useCase.Execute("userA", "name", "Sara", now.AddMinutes(5));

        var entry = repository.Get("userA", "name");

        Assert.NotNull(entry);
        Assert.Equal("Sara", entry.Value);
        Assert.Equal(now, entry.CreatedAt);
        Assert.Equal(now.AddMinutes(5), entry.UpdatedAt);
    }

    [Fact]
    public void Get_ShouldReturnExistingEntry()
    {
        var repository = new FakeKeyValueEntryRepository();
        var setUseCase = new SetEntryUseCase(repository);
        var getUseCase = new GetEntryUseCase(repository);

        setUseCase.Execute(
            "userA", "name", "Ali", DateTimeOffset.UnixEpoch);

        var entry = getUseCase.Execute("userA", "name");

        Assert.NotNull(entry);
        Assert.Equal("Ali", entry.Value);
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
        var setUseCase = new SetEntryUseCase(repository);
        var deleteUseCase = new DeleteEntryUseCase(repository);

        setUseCase.Execute(
            "userA", "name", "Ali", DateTimeOffset.UnixEpoch);

        bool deleted = deleteUseCase.Execute("userA", "name");

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