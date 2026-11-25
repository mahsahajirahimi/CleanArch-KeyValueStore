using KeyValueStore.Domain.Security;

namespace KeyValueStore.Infrastructure.Security;

public sealed record BootstrapAdminResult(
    Guid UserId,
    Guid KeyId,
    string PlaintextKey);

public sealed class FirstAdminBootstrapper
{
    private readonly JsonUserAccountRepository _users;
    private readonly JsonApiKeyCredentialRepository _credentials;
    private readonly ApiKeyTokenService _tokens;

    public FirstAdminBootstrapper(
        JsonUserAccountRepository users,
        JsonApiKeyCredentialRepository credentials,
        ApiKeyTokenService tokens)
    {
        _users = users
            ?? throw new ArgumentNullException(nameof(users));

        _credentials = credentials
            ?? throw new ArgumentNullException(nameof(credentials));

        _tokens = tokens
            ?? throw new ArgumentNullException(nameof(tokens));
    }

    public BootstrapAdminResult Execute(
        string keyspace,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(keyspace))
        {
            throw new ArgumentException(
                "Keyspace cannot be empty.",
                nameof(keyspace));
        }

        if (_users.HasAny())
        {
            throw new InvalidOperationException(
                "Bootstrap is only allowed when no users exist.");
        }

        var admin = new UserAccount(
            Guid.NewGuid(),
            keyspace,
            UserRole.Admin);

        var (credential, plaintextKey) =
            _tokens.Issue(admin.Id, now);

        _credentials.Save(credential);

        _users.Save(admin);

        return new BootstrapAdminResult(
            admin.Id,
            credential.Id,
            plaintextKey);
    }
}