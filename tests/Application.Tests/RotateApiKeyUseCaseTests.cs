using KeyValueStore.Application.Abstractions;
using KeyValueStore.Application.UseCases;
using KeyValueStore.Domain.Security;
using Xunit;

namespace KeyValueStore.Application.Tests;

public sealed class RotateApiKeyUseCaseTests
{
    private readonly FakeUsers _users = new();
    private readonly FakeCredentials _credentials = new();
    private readonly FakeTokens _tokens = new();

    private RotateApiKeyUseCase CreateUseCase() =>
        new(_users, _credentials, _tokens);

    [Fact]
    public void Execute_ShouldRevokeOldKeyAndIssueReplacement()
    {
        var now = DateTimeOffset.UtcNow;

        var admin = AddUser("userA", UserRole.Admin);
        var owner = AddUser("userA", UserRole.Reader);
        var old = AddCredential(owner.Id, now.AddMinutes(-5));

        IssuedApiKey issued = CreateUseCase().Execute(
            admin.Id, old.Id, now);

        Assert.Equal(owner.Id, issued.UserId);
        Assert.Equal("test-only-secret", issued.PlaintextKey);
        Assert.NotEqual(old.Id, issued.KeyId);

        Assert.True(_credentials.GetById(old.Id)!.IsRevoked);
        Assert.Equal(
            now,
            _credentials.GetById(old.Id)!.RevokedAt);

        var replacement = _credentials.GetById(issued.KeyId);

        Assert.NotNull(replacement);
        Assert.False(replacement.IsRevoked);
        Assert.Equal(owner.Id, replacement.UserId);
        Assert.Equal(1, _credentials.RotateCount);
        Assert.Equal(1, _tokens.IssueCount);
    }

    [Theory]
    [InlineData(UserRole.Reader)]
    [InlineData(UserRole.Writer)]
    public void Execute_ShouldRejectNonAdmin(UserRole role)
    {
        var now = DateTimeOffset.UtcNow;

        var requester = AddUser("userA", role);
        var owner = AddUser("userA", UserRole.Reader);
        var old = AddCredential(owner.Id, now);

        Assert.Throws<UnauthorizedAccessException>(() =>
            CreateUseCase().Execute(
                requester.Id,
                old.Id,
                now.AddMinutes(1)));

        Assert.Equal(0, _tokens.IssueCount);
        Assert.False(_credentials.GetById(old.Id)!.IsRevoked);
    }

    [Fact]
    public void Execute_ShouldRejectDifferentKeyspace()
    {
        var now = DateTimeOffset.UtcNow;

        var admin = AddUser("userA", UserRole.Admin);
        var owner = AddUser("userB", UserRole.Reader);
        var old = AddCredential(owner.Id, now);

        Assert.Throws<UnauthorizedAccessException>(() =>
            CreateUseCase().Execute(
                admin.Id, old.Id, now.AddMinutes(1)));

        Assert.Equal(0, _tokens.IssueCount);
    }

    [Fact]
    public void Execute_ShouldRejectRevokedCredential()
    {
        var now = DateTimeOffset.UtcNow;

        var admin = AddUser("userA", UserRole.Admin);
        var owner = AddUser("userA", UserRole.Reader);
        var old = AddCredential(owner.Id, now.AddMinutes(-10));

        old.Revoke(now.AddMinutes(-5));
        _credentials.Save(old);

        Assert.Throws<InvalidOperationException>(() =>
            CreateUseCase().Execute(admin.Id, old.Id, now));

        Assert.Equal(0, _tokens.IssueCount);
        Assert.Equal(0, _credentials.RotateCount);
    }

    [Fact]
    public void Execute_ShouldRejectInactiveOwner()
    {
        var now = DateTimeOffset.UtcNow;

        var admin = AddUser("userA", UserRole.Admin);
        var owner = AddUser("userA", UserRole.Reader);
        var old = AddCredential(owner.Id, now);

        owner.Deactivate();

        Assert.Throws<UnauthorizedAccessException>(() =>
            CreateUseCase().Execute(
                admin.Id, old.Id, now.AddMinutes(1)));

        Assert.False(_credentials.GetById(old.Id)!.IsRevoked);
        Assert.Equal(0, _tokens.IssueCount);
    }

    [Fact]
    public void Execute_ShouldPreserveOldKeyIfStorageFails()
    {
        var now = DateTimeOffset.UtcNow;

        var admin = AddUser("userA", UserRole.Admin);
        var owner = AddUser("userA", UserRole.Reader);
        var old = AddCredential(owner.Id, now.AddMinutes(-5));

        _credentials.FailOnRotate = true;

        Assert.Throws<IOException>(() =>
            CreateUseCase().Execute(admin.Id, old.Id, now));

        Assert.False(_credentials.GetById(old.Id)!.IsRevoked);
        Assert.Single(_credentials.GetByUserId(owner.Id));

        Assert.Equal(1, _tokens.IssueCount);
    }

    [Fact]
    public void Execute_ShouldRejectUnknownCredential()
    {
        var admin = AddUser("userA", UserRole.Admin);

        Assert.Throws<UnauthorizedAccessException>(() =>
            CreateUseCase().Execute(
                admin.Id,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow));

        Assert.Equal(0, _tokens.IssueCount);
    }

    private UserAccount AddUser(string keyspace, UserRole role)
    {
        var user = new UserAccount(
            Guid.NewGuid(), keyspace, role);

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

    private sealed class FakeUsers : IUserAccountRepository
    {
        private readonly Dictionary<Guid, UserAccount> _items = new();

        public UserAccount? GetById(Guid userId) =>
            _items.GetValueOrDefault(userId);

        public void Save(UserAccount user) =>
            _items[user.Id] = user;
    }

    private sealed class FakeTokens : IApiKeyTokenService
    {
        public int IssueCount { get; private set; }

        public (
            ApiKeyCredential Credential,
            string PlaintextKey
        ) Issue(Guid userId, DateTimeOffset createdAt)
        {
            IssueCount++;

            var credential = new ApiKeyCredential(
                Guid.NewGuid(),
                userId,
                new string('B', 64),
                createdAt);

            return (credential, "test-only-secret");
        }
    }

    private sealed class FakeCredentials
        : IApiKeyCredentialRepository
    {
        private Dictionary<Guid, ApiKeyCredential> _items = new();

        public int RotateCount { get; private set; }
        public bool FailOnRotate { get; set; }

        public ApiKeyCredential? GetById(Guid keyId) =>
            _items.TryGetValue(keyId, out var credential)
                ? Clone(credential)
                : null;

        public IReadOnlyList<ApiKeyCredential> GetByUserId(
            Guid userId) =>
            _items.Values
                .Where(c => c.UserId == userId)
                .Select(Clone)
                .ToList();

        public void Save(ApiKeyCredential credential)
        {
            _items[credential.Id] = Clone(credential);
        }

        public void Rotate(
            Guid oldKeyId,
            ApiKeyCredential replacement,
            DateTimeOffset now)
        {
            if (FailOnRotate)
                throw new IOException("Simulated rotation failure.");

            var old = GetById(oldKeyId)
                ?? throw new InvalidOperationException(
                    "Original key not found.");

            old.Revoke(now);

            var updated =
                new Dictionary<Guid, ApiKeyCredential>(_items)
                {
                    [oldKeyId] = old,
                    [replacement.Id] = Clone(replacement)
                };

            _items = updated;
            RotateCount++;
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
    }
}