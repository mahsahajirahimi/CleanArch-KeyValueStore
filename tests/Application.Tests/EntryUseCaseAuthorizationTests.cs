using KeyValueStore.Application.Tests.Fakes;
using KeyValueStore.Application.UseCases;
using KeyValueStore.Domain.Aggregates;
using KeyValueStore.Domain.Security;
using Xunit;

namespace KeyValueStore.Application.Tests;

public sealed class EntryUseCaseAuthorizationTests
{
    [Fact]
    public void Reader_CanReadButCannotSetOrDelete()
    {
        var context = CreateContext(UserRole.Reader);

        context.Repository.Save(
            "team-a",
            new KeyValueEntry(
                "secret",
                "original",
                DateTimeOffset.UtcNow));

        var entry = context.Get.Execute(
            context.User.Id,
            "team-a",
            "secret");

        Assert.NotNull(entry);
        Assert.Equal("original", entry.Value);

        Assert.Throws<UnauthorizedAccessException>(() =>
            context.Set.Execute(
                context.User.Id,
                "team-a",
                "secret",
                "modified",
                DateTimeOffset.UtcNow));

        Assert.Throws<UnauthorizedAccessException>(() =>
            context.Delete.Execute(
                context.User.Id,
                "team-a",
                "secret"));

        Assert.Null(context.Repository.LastUpsert);

        Assert.Equal(
            "original",
            context.Repository.Get("team-a", "secret")?.Value);
    }

    [Theory]
    [InlineData(UserRole.Writer)]
    [InlineData(UserRole.Admin)]
    public void WriterAndAdmin_CanExecuteAllOperations(
        UserRole role)
    {
        var context = CreateContext(role);

        context.Set.Execute(
            context.User.Id,
            "team-a",
            "name",
            "Ali",
            DateTimeOffset.UtcNow);

        Assert.NotNull(context.Repository.LastUpsert);

        context.Repository.Save(
            "team-a",
            new KeyValueEntry(
                "name",
                "Ali",
                DateTimeOffset.UtcNow));

        var entry = context.Get.Execute(
            context.User.Id,
            "team-a",
            "name");

        Assert.NotNull(entry);
        Assert.Equal("Ali", entry.Value);

        Assert.True(context.Delete.Execute(
            context.User.Id,
            "team-a",
            "name"));

        Assert.Null(
            context.Repository.Get("team-a", "name"));
    }

    [Theory]
    [InlineData(UserRole.Reader)]
    [InlineData(UserRole.Writer)]
    [InlineData(UserRole.Admin)]
    public void ForeignKeyspace_ShouldRejectAllOperations(
        UserRole role)
    {
        var context = CreateContext(role);

        context.Repository.Save(
            "team-b",
            new KeyValueEntry(
                "secret",
                "protected",
                DateTimeOffset.UtcNow));

        AssertAllOperationsDenied(
            context,
            context.User.Id,
            "team-b");

        Assert.Equal(
            "protected",
            context.Repository.Get("team-b", "secret")?.Value);
    }

    [Fact]
    public void InactiveUser_ShouldNotAccessEntries()
    {
        var context = CreateContext(UserRole.Admin);

        context.User.Deactivate();
        context.Users.Save(context.User);

        AssertAllOperationsDenied(
            context,
            context.User.Id,
            "team-a");
    }

    [Fact]
    public void UnknownUser_ShouldNotAccessEntries()
    {
        var context = CreateContext(UserRole.Admin);

        AssertAllOperationsDenied(
            context,
            Guid.NewGuid(),
            "team-a");
    }

    [Fact]
    public void EmptyRequesterId_ShouldNotAccessEntries()
    {
        var context = CreateContext(UserRole.Admin);

        AssertAllOperationsDenied(
            context,
            Guid.Empty,
            "team-a");
    }

    private static void AssertAllOperationsDenied(
        (
            FakeKeyValueEntryRepository Repository,
            FakeUserAccountRepository Users,
            UserAccount User,
            SetEntryUseCase Set,
            GetEntryUseCase Get,
            DeleteEntryUseCase Delete
        ) context,
        Guid requesterId,
        string keyspace)
    {
        Assert.Throws<UnauthorizedAccessException>(() =>
            context.Set.Execute(
                requesterId,
                keyspace,
                "secret",
                "modified",
                DateTimeOffset.UtcNow));

        Assert.Throws<UnauthorizedAccessException>(() =>
            context.Get.Execute(
                requesterId,
                keyspace,
                "secret"));

        Assert.Throws<UnauthorizedAccessException>(() =>
            context.Delete.Execute(
                requesterId,
                keyspace,
                "secret"));

        Assert.Null(context.Repository.LastUpsert);
    }

    private static (
        FakeKeyValueEntryRepository Repository,
        FakeUserAccountRepository Users,
        UserAccount User,
        SetEntryUseCase Set,
        GetEntryUseCase Get,
        DeleteEntryUseCase Delete
    ) CreateContext(UserRole role)
    {
        var repository = new FakeKeyValueEntryRepository();
        var users = new FakeUserAccountRepository();

        var user = new UserAccount(
            Guid.NewGuid(),
            "team-a",
            role);

        users.Save(user);

        var authorization =
            new AuthorizeEntryOperationUseCase(users);

        return (
            repository,
            users,
            user,
            new SetEntryUseCase(repository, authorization),
            new GetEntryUseCase(repository, authorization),
            new DeleteEntryUseCase(repository, authorization));
    }
}