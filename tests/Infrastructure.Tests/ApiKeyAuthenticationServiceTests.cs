using KeyValueStore.Domain.Security;
using KeyValueStore.Infrastructure.Security;
using Xunit;

namespace KeyValueStore.Infrastructure.Tests;

public sealed class ApiKeyAuthenticationServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"ApiKeyAuthTests-{Guid.NewGuid():N}");

    private readonly JsonUserAccountRepository _users;
    private readonly JsonApiKeyCredentialRepository _credentials;
    private readonly ApiKeyTokenService _tokens = new();
    private readonly ApiKeyAuthenticationService _authentication;

    public ApiKeyAuthenticationServiceTests()
    {
        _users = new JsonUserAccountRepository(
            Path.Combine(_directory, "users.json"));

        _credentials = new JsonApiKeyCredentialRepository(
            Path.Combine(_directory, "credentials.json"));

        _authentication = new ApiKeyAuthenticationService(
            _users, _credentials, _tokens);
    }

    [Fact]
    public void Authenticate_ShouldReturnUserForValidKey()
    {
        var (user, credential, key) = IssueKey(UserRole.Admin);

        var result = _authentication.Authenticate(key);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(credential.Id, result.KeyId);
        Assert.Equal("userA", result.Keyspace);
        Assert.Equal(UserRole.Admin, result.Role);
    }

    [Fact]
    public void Authenticate_ShouldRejectMalformedKey()
    {
        Assert.Null(_authentication.Authenticate(null));
        Assert.Null(_authentication.Authenticate(""));
        Assert.Null(_authentication.Authenticate("invalid"));
    }

    [Fact]
    public void Authenticate_ShouldRejectModifiedSecret()
    {
        var (_, _, key) = IssueKey(UserRole.Reader);

        char replacement = key[^1] == 'A' ? 'B' : 'A';
        string modified = key[..^1] + replacement;

        Assert.Null(_authentication.Authenticate(modified));
        Assert.NotNull(_authentication.Authenticate(key));
    }

    [Fact]
    public void Authenticate_ShouldRejectRevokedKey()
    {
        var (_, credential, key) = IssueKey(UserRole.Reader);

        var stored = _credentials.GetById(credential.Id);
        Assert.NotNull(stored);

        stored.Revoke(DateTimeOffset.UtcNow.AddMinutes(1));
        _credentials.Save(stored);

        Assert.Null(_authentication.Authenticate(key));
    }

    [Fact]
    public void Authenticate_ShouldRejectInactiveUser()
    {
        var (user, _, key) = IssueKey(UserRole.Reader);

        user.Deactivate();
        _users.Save(user);

        Assert.Null(_authentication.Authenticate(key));
    }

    [Fact]
    public void Authenticate_ShouldRejectUnpersistedCredential()
    {
        var user = new UserAccount(
            Guid.NewGuid(), "userA", UserRole.Reader);

        _users.Save(user);

        var issued = _tokens.Issue(
            user.Id, DateTimeOffset.UtcNow);

        // Credential was generated but never persisted.
        Assert.Null(_authentication.Authenticate(
            issued.PlaintextKey));
    }

    [Fact]
    public void Authenticate_ShouldRejectMissingUser()
    {
        var issued = _tokens.Issue(
            Guid.NewGuid(), DateTimeOffset.UtcNow);

        _credentials.Save(issued.Credential);

        Assert.Null(_authentication.Authenticate(
            issued.PlaintextKey));
    }

    [Fact]
    public void Authenticate_ShouldUseCurrentUserRole()
    {
        var (user, _, key) = IssueKey(UserRole.Reader);

        user.ChangeRole(UserRole.Writer);
        _users.Save(user);

        var result = _authentication.Authenticate(key);

        Assert.NotNull(result);
        Assert.Equal(UserRole.Writer, result.Role);
    }

    private (
        UserAccount User,
        ApiKeyCredential Credential,
        string Key
    ) IssueKey(UserRole role)
    {
        var user = new UserAccount(
            Guid.NewGuid(), "userA", role);

        _users.Save(user);

        var issued = _tokens.Issue(
            user.Id, DateTimeOffset.UtcNow);

        _credentials.Save(issued.Credential);

        return (
            user,
            issued.Credential,
            issued.PlaintextKey);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}