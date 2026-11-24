using KeyValueStore.Application.Abstractions;
using KeyValueStore.Application.UseCases;
using KeyValueStore.Domain.Security;
using Xunit;

namespace KeyValueStore.Application.Tests;

public sealed class RevokeApiKeyUseCaseTests
{
    private readonly FakeUserRepository _users = new();
    private readonly FakeCredentialRepository _credentials = new();
    private readonly RevokeApiKeyUseCase _useCase;

    public RevokeApiKeyUseCaseTests()
    {
        _useCase = new RevokeApiKeyUseCase(
            _users,
            _credentials);
    }

    [Fact]
    public void Execute_ShouldRevokeCredential()
    {
        var now = DateTimeOffset.UtcNow;

        var admin = AddUser("userA", UserRole.Admin);
        var owner = AddUser("userA", UserRole.Reader);

        var first = AddCredential(
            owner.Id, now.AddMinutes(-5));

        var second = AddCredential(
            owner.Id, now.AddMinutes(-5));

        bool result = _useCase.Execute(
            admin.Id, first.Id, now);

        Assert.True(result);

        var revoked = _credentials.GetById(first.Id);
        var remaining = _credentials.GetById(second.Id);

        Assert.NotNull(revoked);
        Assert.True(revoked.IsRevoked);
        Assert.Equal(now, revoked.RevokedAt);

        Assert.NotNull(remaining);
        Assert.False(remaining.IsRevoked);
    }

    [Fact]
    public void Execute_ShouldReturnFalseForAlreadyRevokedKey()
    {
        var now = DateTimeOffset.UtcNow;

        var admin = AddUser("userA", UserRole.Admin);
        var owner = AddUser("userA", UserRole.Reader);

        var credential = AddCredential(
            owner.Id, now.AddMinutes(-10));

        credential.Revoke(now.AddMinutes(-5));
        _credentials.Save(credential);

        int previousSaveCount = _credentials.SaveCount;

        bool result = _useCase.Execute(
            admin.Id, credential.Id, now);

        Assert.False(result);
        Assert.Equal(
            previousSaveCount,
            _credentials.SaveCount);

        Assert.Equal(
            now.AddMinutes(-5),
            _credentials.GetById(credential.Id)?.RevokedAt);
    }

    [Theory]
    [InlineData(UserRole.Reader)]
    [InlineData(UserRole.Writer)]
    public void Execute_ShouldRejectNonAdminRequester(
        UserRole role)
    {
        var now = DateTimeOffset.UtcNow;

        var requester = AddUser("userA", role);
        var owner = AddUser("userA", UserRole.Reader);
        var credential = AddCredential(owner.Id, now);

        Assert.Throws<UnauthorizedAccessException>(() =>
            _useCase.Execute(
                requester.Id,
                credential.Id,
                now.AddMinutes(1)));

        Assert.False(
            _credentials.GetById(credential.Id)!.IsRevoked);
    }

    [Fact]
    public void Execute_ShouldRejectInactiveAdmin()
    {
        var now = DateTimeOffset.UtcNow;

        var admin = AddUser("userA", UserRole.Admin);
        var owner = AddUser("userA", UserRole.Reader);
        var credential = AddCredential(owner.Id, now);

        admin.Deactivate();
        _users.Save(admin);

        Assert.Throws<UnauthorizedAccessException>(() =>
            _useCase.Execute(
                admin.Id,
                credential.Id,
                now.AddMinutes(1)));

        Assert.False(
            _credentials.GetById(credential.Id)!.IsRevoked);
    }

    [Fact]
    public void Execute_ShouldRejectDifferentKeyspace()
    {
        var now = DateTimeOffset.UtcNow;

        var admin = AddUser("userA", UserRole.Admin);
        var owner = AddUser("userB", UserRole.Reader);
        var credential = AddCredential(owner.Id, now);

        Assert.Throws<UnauthorizedAccessException>(() =>
            _useCase.Execute(
                admin.Id,
                credential.Id,
                now.AddMinutes(1)));

        Assert.False(
            _credentials.GetById(credential.Id)!.IsRevoked);
    }

    [Fact]
    public void Execute_ShouldRejectUnknownCredential()
    {
        var admin = AddUser("userA", UserRole.Admin);

        Assert.Throws<UnauthorizedAccessException>(() =>
            _useCase.Execute(
                admin.Id,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Execute_ShouldAllowRevokingInactiveUsersKey()
    {
        var now = DateTimeOffset.UtcNow;

        var admin = AddUser("userA", UserRole.Admin);
        var owner = AddUser("userA", UserRole.Reader);
        var credential = AddCredential(owner.Id, now);

        owner.Deactivate();
        _users.Save(owner);

        Assert.True(_useCase.Execute(
            admin.Id,
            credential.Id,
            now.AddMinutes(1)));

        Assert.True(
            _credentials.GetById(credential.Id)!.IsRevoked);
    }

    [Fact]
    public void Execute_ShouldNotPersistIfSaveFails()
    {
        var now = DateTimeOffset.UtcNow;

        var admin = AddUser("userA", UserRole.Admin);
        var owner = AddUser("userA", UserRole.Reader);
        var credential = AddCredential(owner.Id, now);

        _credentials.FailOnSave = true;

        Assert.Throws<IOException>(() =>
            _useCase.Execute(
                admin.Id,
                credential.Id,
                now.AddMinutes(1)));

        Assert.False(
            _credentials.GetById(credential.Id)!.IsRevoked);
    }

    [Fact]
    public void Execute_ShouldRejectEmptyIdentifiers()
    {
        Assert.Throws<ArgumentException>(() =>
            _useCase.Execute(
                Guid.Empty,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow));

        Assert.Throws<ArgumentException>(() =>
            _useCase.Execute(
                Guid.NewGuid(),
                Guid.Empty,
                DateTimeOffset.UtcNow));
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

    private ApiKeyCredential AddCredential(
        Guid userId,
        DateTimeOffset createdAt)
    {
        var credential = new ApiKeyCredential(
            Guid.NewGuid(),
            userId,
            new string('A', 64),
            createdAt);

        _credentials.Save(credential);
        return credential;
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

    private sealed class FakeCredentialRepository
        : IApiKeyCredentialRepository
    {
        private readonly Dictionary<Guid, ApiKeyCredential>
            _credentials = new();

        public int SaveCount { get; private set; }

        public bool FailOnSave { get; set; }

        public ApiKeyCredential? GetById(Guid keyId)
        {
            return _credentials.TryGetValue(
                keyId,
                out var credential)
                ? Clone(credential)
                : null;
        }

        public IReadOnlyList<ApiKeyCredential> GetByUserId(
            Guid userId)
        {
            return _credentials.Values
                .Where(c => c.UserId == userId)
                .Select(Clone)
                .ToList();
        }

        public void Save(ApiKeyCredential credential)
        {
            if (FailOnSave)
            {
                throw new IOException(
                    "Simulated storage failure.");
            }

            _credentials[credential.Id] = Clone(credential);
            SaveCount++;
        }

        private static ApiKeyCredential Clone(
            ApiKeyCredential source)
        {
            var copy = new ApiKeyCredential(
                source.Id,
                source.UserId,
                source.SecretHash,
                source.CreatedAt);

            if (source.RevokedAt is not null)
                copy.Revoke(source.RevokedAt.Value);

            return copy;
        }

        public void Rotate(
            Guid oldKeyId,
            ApiKeyCredential replacement,
            DateTimeOffset now)
        {
            throw new NotSupportedException(
                "Rotation is not used by this test.");
        }
    }
}