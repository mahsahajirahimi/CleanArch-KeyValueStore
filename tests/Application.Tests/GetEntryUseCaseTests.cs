
using KeyValueStore.Application.Tests.Fakes;
using KeyValueStore.Application.UseCases;
using KeyValueStore.Domain.Aggregates;
using Xunit;

namespace KeyValueStore.Application.Tests;

public class GetEntryUseCaseTests
{
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
}