using KeyValueStore.Application.Abstractions;
using KeyValueStore.Application.UseCases;
using KeyValueStore.Domain.Security;
using Xunit;

namespace KeyValueStore.Application.Tests;

public sealed class IssueApiKeyUseCaseTests
{
    [Fact]
    public void Execute_ShouldIssueKeyForAuthorizedAdmin()
    {
        var users = new FakeUserRepository();
        var credentials = new FakeCredentialRepository();
        var tokens = new FakeTokenService();

        var admin = CreateUser("userA", UserRole.Admin);
        var reader = CreateUser("userA", UserRole.Reader);

        users.Add(admin);
        users.Add(reader);

        var useCase = new IssueApiKeyUseCase(
            users, credentials, tokens);

        var now = DateTimeOffset.UtcNow;

        IssuedApiKey result = useCase.Execute(
            admin.Id,
            reader.Id,
            now);

        Assert.Equal(reader.Id, result.UserId);
        Assert.Equal("test-only-key", result.PlaintextKey);
        Assert.Equal(now, result.CreatedAt);

        var saved = credentials.GetById(result.KeyId);

        Assert.NotNull(saved);
        Assert.Equal(reader.Id, saved.UserId);
        Assert.False(saved.IsRevoked);
        Assert.Equal(1, tokens.IssueCount);
    }

    [Theory]
    [InlineData(UserRole.Reader)]
    [InlineData(UserRole.Writer)]
    public void Execute_ShouldRejectNonAdminRequester(
        UserRole role)
    {
        var users = new FakeUserRepository();
        var credentials = new FakeCredentialRepository();
        var tokens = new FakeTokenService();

        var requester = CreateUser("userA", role);
        var target = CreateUser("userA", UserRole.Reader);

        users.Add(requester);
        users.Add(target);

        var useCase = new IssueApiKeyUseCase(
            users, credentials, tokens);

        Assert.Throws<UnauthorizedAccessException>(() =>
            useCase.Execute(
                requester.Id,
                target.Id,
                DateTimeOffset.UtcNow));

        Assert.Equal(0, tokens.IssueCount);
        Assert.Empty(credentials.GetByUserId(target.Id));
    }

    [Fact]
    public void Execute_ShouldRejectDifferentKeyspace()
    {
        var users = new FakeUserRepository();
        var credentials = new FakeCredentialRepository();
        var tokens = new FakeTokenService();

        var admin = CreateUser("userA", UserRole.Admin);
        var target = CreateUser("userB", UserRole.Reader);

        users.Add(admin);
        users.Add(target);

        var useCase = new IssueApiKeyUseCase(
            users, credentials, tokens);

        Assert.Throws<UnauthorizedAccessException>(() =>
            useCase.Execute(
                admin.Id,
                target.Id,
                DateTimeOffset.UtcNow));

        Assert.Equal(0, tokens.IssueCount);
    }

    [Fact]
    public void Execute_ShouldRejectInactiveAdmin()
    {
        var users = new FakeUserRepository();
        var tokens = new FakeTokenService();

        var admin = CreateUser("userA", UserRole.Admin);
        var target = CreateUser("userA", UserRole.Reader);

        admin.Deactivate();

        users.Add(admin);
        users.Add(target);

        var useCase = new IssueApiKeyUseCase(
            users,
            new FakeCredentialRepository(),
            tokens);

        Assert.Throws<UnauthorizedAccessException>(() =>
            useCase.Execute(
                admin.Id,
                target.Id,
                DateTimeOffset.UtcNow));

        Assert.Equal(0, tokens.IssueCount);
    }

    [Fact]
    public void Execute_ShouldRejectInactiveTarget()
    {
        var users = new FakeUserRepository();
        var tokens = new FakeTokenService();

        var admin = CreateUser("userA", UserRole.Admin);
        var target = CreateUser("userA", UserRole.Reader);

        target.Deactivate();

        users.Add(admin);
        users.Add(target);

        var useCase = new IssueApiKeyUseCase(
            users,
            new FakeCredentialRepository(),
            tokens);

        Assert.Throws<UnauthorizedAccessException>(() =>
            useCase.Execute(
                admin.Id,
                target.Id,
                DateTimeOffset.UtcNow));

        Assert.Equal(0, tokens.IssueCount);
    }

    [Fact]
    public void Execute_ShouldRejectUnknownTarget()
    {
        var users = new FakeUserRepository();
        var tokens = new FakeTokenService();

        var admin = CreateUser("userA", UserRole.Admin);
        users.Add(admin);

        var useCase = new IssueApiKeyUseCase(
            users,
            new FakeCredentialRepository(),
            tokens);

        Assert.Throws<UnauthorizedAccessException>(() =>
            useCase.Execute(
                admin.Id,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow));

        Assert.Equal(0, tokens.IssueCount);
    }

    [Fact]
    public void Execute_ShouldPropagateStorageFailure()
    {
        var users = new FakeUserRepository();
        var credentials = new FakeCredentialRepository
        {
            FailOnSave = true
        };

        var admin = CreateUser("userA", UserRole.Admin);
        var target = CreateUser("userA", UserRole.Reader);

        users.Add(admin);
        users.Add(target);

        var useCase = new IssueApiKeyUseCase(
            users,
            credentials,
            new FakeTokenService());

        Assert.Throws<IOException>(() =>
            useCase.Execute(
                admin.Id,
                target.Id,
                DateTimeOffset.UtcNow));

        Assert.Empty(credentials.GetByUserId(target.Id));
    }

    private static UserAccount CreateUser(
        string keyspace,
        UserRole role)
    {
        return new UserAccount(
            Guid.NewGuid(),
            keyspace,
            role);
    }

    private sealed class FakeUserRepository
        : IUserAccountRepository
    {
        private readonly Dictionary<Guid, UserAccount> _users = new();

        public void Add(UserAccount user)
        {
            _users.Add(user.Id, user);
        }

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

        public bool FailOnSave { get; set; }

        public ApiKeyCredential? GetById(Guid keyId)
        {
            return _credentials.GetValueOrDefault(keyId);
        }

        public IReadOnlyList<ApiKeyCredential> GetByUserId(
            Guid userId)
        {
            return _credentials.Values
                .Where(c => c.UserId == userId)
                .ToList();
        }

        public void Save(ApiKeyCredential credential)
        {
            if (FailOnSave)
                throw new IOException("Simulated storage failure.");

            _credentials[credential.Id] = credential;
        }
    }

    private sealed class FakeTokenService
        : IApiKeyTokenService
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
                new string('A', 64),
                createdAt);

            return (credential, "test-only-key");
        }
    }
}