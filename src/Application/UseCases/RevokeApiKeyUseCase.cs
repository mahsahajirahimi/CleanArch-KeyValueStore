using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Security;

namespace KeyValueStore.Application.UseCases;

public sealed class RevokeApiKeyUseCase
{
    private readonly IUserAccountRepository _users;
    private readonly IApiKeyCredentialRepository _credentials;

    public RevokeApiKeyUseCase(
        IUserAccountRepository users,
        IApiKeyCredentialRepository credentials)
    {
        _users = users
            ?? throw new ArgumentNullException(nameof(users));

        _credentials = credentials
            ?? throw new ArgumentNullException(nameof(credentials));
    }

    public bool Execute(
        Guid requesterId,
        Guid keyId,
        DateTimeOffset now)
    {
        if (requesterId == Guid.Empty)
        {
            throw new ArgumentException(
                "Requester ID cannot be empty.",
                nameof(requesterId));
        }

        if (keyId == Guid.Empty)
        {
            throw new ArgumentException(
                "API key ID cannot be empty.",
                nameof(keyId));
        }

        UserAccount? requester = _users.GetById(requesterId);

        if (requester is null ||
            !requester.IsActive ||
            requester.Role != UserRole.Admin)
        {
            throw new UnauthorizedAccessException(
                "Not authorized to revoke API keys.");
        }

        ApiKeyCredential? credential =
            _credentials.GetById(keyId);

        if (credential is null)
        {
            throw new UnauthorizedAccessException(
                "Not authorized to revoke API keys.");
        }

        UserAccount? owner = _users.GetById(credential.UserId);

        if (owner is null ||
            !string.Equals(
                requester.Keyspace,
                owner.Keyspace,
                StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Not authorized to revoke API keys.");
        }

        if (credential.IsRevoked)
            return false;

        credential.Revoke(now);
        _credentials.Save(credential);

        return true;
    }
}