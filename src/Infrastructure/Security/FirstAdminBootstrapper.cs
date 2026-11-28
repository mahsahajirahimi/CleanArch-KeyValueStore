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
        ValidateKeyspace(keyspace);

        if (_users.HasAny())
        {
            throw new InvalidOperationException(
                "Bootstrap is only allowed when no users exist.");
        }

        return CreateAdmin(keyspace, now);
    }

    public BootstrapAdminResult ProvisionAdditionalKeyspace(
        string keyspace,
        DateTimeOffset now)
    {
        ValidateKeyspace(keyspace);

        if (!_users.HasAny())
        {
            throw new InvalidOperationException(
                "Run the initial bootstrap first.");
        }

        if (_users.HasKeyspace(keyspace))
        {
            throw new InvalidOperationException(
                "Keyspace already exists.");
        }

        return CreateAdmin(keyspace, now);
    }

    private BootstrapAdminResult CreateAdmin(
        string keyspace,
        DateTimeOffset now)
    {
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

    private static void ValidateKeyspace(string keyspace)
    {
        if (string.IsNullOrWhiteSpace(keyspace))
        {
            throw new ArgumentException(
                "Keyspace cannot be empty.",
                nameof(keyspace));
        }
    }
}