using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Security;

namespace KeyValueStore.Infrastructure.Security;

public sealed record AuthenticatedApiKeyUser(
    Guid UserId,
    Guid KeyId,
    string Keyspace,
    UserRole Role);

public sealed class ApiKeyAuthenticationService
{
    private readonly IUserAccountRepository _users;
    private readonly IApiKeyCredentialRepository _credentials;
    private readonly ApiKeyTokenService _tokens;

    public ApiKeyAuthenticationService(
        IUserAccountRepository users,
        IApiKeyCredentialRepository credentials,
        ApiKeyTokenService tokens)
    {
        _users = users
            ?? throw new ArgumentNullException(nameof(users));

        _credentials = credentials
            ?? throw new ArgumentNullException(nameof(credentials));

        _tokens = tokens
            ?? throw new ArgumentNullException(nameof(tokens));
    }

    public AuthenticatedApiKeyUser? Authenticate(
        string? presentedKey)
    {
        if (!_tokens.TryGetKeyId(
            presentedKey,
            out Guid keyId))
        {
            return null;
        }

        ApiKeyCredential? credential =
            _credentials.GetById(keyId);

        if (credential is null ||
            !_tokens.Verify(credential, presentedKey))
        {
            return null;
        }

        UserAccount? user =
            _users.GetById(credential.UserId);

        if (user is null || !user.IsActive)
            return null;

        return new AuthenticatedApiKeyUser(
            user.Id,
            credential.Id,
            user.Keyspace,
            user.Role);
    }
}