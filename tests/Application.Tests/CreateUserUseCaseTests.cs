using KeyValueStore.Application.Abstractions;
using KeyValueStore.Application.UseCases;
using KeyValueStore.Domain.Security;
using Xunit;

namespace KeyValueStore.Application.Tests;

public sealed class CreateUserUseCaseTests
{
    private readonly FakeUserRepository _users = new();

    private CreateUserUseCase CreateUseCase() =>
        new(_users);

    [Theory]
    [InlineData(UserRole.Reader)]
    [InlineData(UserRole.Writer)]
    [InlineData(UserRole.Admin)]
    public void Execute_ShouldCreateUserWithRequestedRole(
        UserRole role)
    {
        var admin = AddUser("userA", UserRole.Admin);

        CreatedUser result = CreateUseCase().Execute(
            admin.Id,
            role);

        var created = _users.GetById(result.UserId);

        Assert.NotNull(created);
        Assert.NotEqual(admin.Id, created.Id);
        Assert.Equal(role, created.Role);
        Assert.Equal("userA", created.Keyspace);
        Assert.True(created.IsActive);
    }

    [Theory]
    [InlineData(UserRole.Reader)]
    [InlineData(UserRole.Writer)]
    public void Execute_ShouldRejectNonAdmin(
        UserRole role)
    {
        var requester = AddUser("userA", role);

        int originalCount = _users.Count;

        Assert.Throws<UnauthorizedAccessException>(() =>
            CreateUseCase().Execute(
                requester.Id,
                UserRole.Reader));

        Assert.Equal(originalCount, _users.Count);
    }

    [Fact]
    public void Execute_ShouldRejectInactiveAdmin()
    {
        var admin = AddUser("userA", UserRole.Admin);

        admin.Deactivate();
        _users.Save(admin);

        Assert.Throws<UnauthorizedAccessException>(() =>
            CreateUseCase().Execute(
                admin.Id,
                UserRole.Reader));
    }

    [Fact]
    public void Execute_ShouldRejectUnknownRequester()
    {
        Assert.Throws<UnauthorizedAccessException>(() =>
            CreateUseCase().Execute(
                Guid.NewGuid(),
                UserRole.Reader));
    }

    [Fact]
    public void Execute_ShouldRejectEmptyRequesterId()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateUseCase().Execute(
                Guid.Empty,
                UserRole.Reader));
    }

    [Fact]
    public void Execute_ShouldRejectInvalidRole()
    {
        var admin = AddUser("userA", UserRole.Admin);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateUseCase().Execute(
                admin.Id,
                (UserRole)999));
    }

    [Fact]
    public void Execute_ShouldNotCreateUserIfSaveFails()
    {
        var admin = AddUser("userA", UserRole.Admin);

        int originalCount = _users.Count;
        _users.FailOnSave = true;

        Assert.Throws<IOException>(() =>
            CreateUseCase().Execute(
                admin.Id,
                UserRole.Reader));

        Assert.Equal(originalCount, _users.Count);
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

        public int Count => _users.Count;

        public bool FailOnSave { get; set; }

        public UserAccount? GetById(Guid userId)
        {
            return _users.TryGetValue(
                userId,
                out var user)
                ? Clone(user)
                : null;
        }

        public void Save(UserAccount user)
        {
            if (FailOnSave)
            {
                throw new IOException(
                    "Simulated storage failure.");
            }

            _users[user.Id] = Clone(user);
        }

        private static UserAccount Clone(UserAccount source)
        {
            var copy = new UserAccount(
                source.Id,
                source.Keyspace,
                source.Role);

            if (!source.IsActive)
                copy.Deactivate();

            return copy;
        }
    }
}