using KeyValueStore.Application.Abstractions;
using KeyValueStore.Application.UseCases;
using KeyValueStore.Domain.Security;
using Xunit;

namespace KeyValueStore.Application.Tests;

public sealed class AuthorizeEntryOperationUseCaseTests
{
    private readonly FakeUserRepository _users = new();

    private AuthorizeEntryOperationUseCase CreateUseCase() =>
        new(_users);

    [Theory]
    [InlineData(UserRole.Reader, EntryOperation.Read, true)]
    [InlineData(UserRole.Reader, EntryOperation.Write, false)]
    [InlineData(UserRole.Reader, EntryOperation.Delete, false)]
    [InlineData(UserRole.Writer, EntryOperation.Read, true)]
    [InlineData(UserRole.Writer, EntryOperation.Write, true)]
    [InlineData(UserRole.Writer, EntryOperation.Delete, true)]
    [InlineData(UserRole.Admin, EntryOperation.Read, true)]
    [InlineData(UserRole.Admin, EntryOperation.Write, true)]
    [InlineData(UserRole.Admin, EntryOperation.Delete, true)]
    public void CanExecute_ShouldRespectRole(
        UserRole role,
        EntryOperation operation,
        bool expected)
    {
        var user = AddUser("userA", role);

        bool result = CreateUseCase().CanExecute(
            user.Id,
            "userA",
            operation);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(UserRole.Reader)]
    [InlineData(UserRole.Writer)]
    [InlineData(UserRole.Admin)]
    public void CanExecute_ShouldRejectDifferentKeyspace(
        UserRole role)
    {
        var user = AddUser("userA", role);

        bool result = CreateUseCase().CanExecute(
            user.Id,
            "userB",
            EntryOperation.Read);

        Assert.False(result);
    }

    [Fact]
    public void CanExecute_ShouldRejectInactiveUser()
    {
        var user = AddUser("userA", UserRole.Admin);

        user.Deactivate();
        _users.Save(user);

        Assert.False(CreateUseCase().CanExecute(
            user.Id,
            "userA",
            EntryOperation.Read));
    }

    [Fact]
    public void CanExecute_ShouldRejectUnknownUser()
    {
        Assert.False(CreateUseCase().CanExecute(
            Guid.NewGuid(),
            "userA",
            EntryOperation.Read));
    }

    [Fact]
    public void CanExecute_ShouldRejectEmptyRequesterId()
    {
        Assert.False(CreateUseCase().CanExecute(
            Guid.Empty,
            "userA",
            EntryOperation.Read));
    }

    [Fact]
    public void CanExecute_ShouldRejectEmptyKeyspace()
    {
        var user = AddUser("userA", UserRole.Admin);

        Assert.False(CreateUseCase().CanExecute(
            user.Id,
            "",
            EntryOperation.Read));
    }

    private UserAccount AddUser(
        string keyspace,
        UserRole role)
    {
        var user = new UserAccount(
            Guid.NewGuid(),
            keyspace,
            role);

        _users.Save(user);
        return user;
    }

    private sealed class FakeUserRepository
        : IUserAccountRepository
    {
        private readonly Dictionary<Guid, UserAccount>
            _users = new();

        public UserAccount? GetById(Guid userId)
        {
            return _users.GetValueOrDefault(userId);
        }

        public void Save(UserAccount user)
        {
            _users[user.Id] = user;
        }
    }
}