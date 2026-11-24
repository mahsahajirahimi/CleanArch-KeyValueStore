using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Security;

namespace KeyValueStore.Application.UseCases;

public sealed class RotateApiKeyUseCase
{
    private readonly IUserAccountRepository _users;
    private readonly IApiKeyCredentialRepository _credentials;
    private readonly IApiKeyTokenService _tokenService;

    public RotateApiKeyUseCase(
        IUserAccountRepository users,
        IApiKeyCredentialRepository credentials,
        IApiKeyTokenService tokenService)
    {
        _users = users
            ?? throw new ArgumentNullException(nameof(users));

        _credentials = credentials
            ?? throw new ArgumentNullException(nameof(credentials));

        _tokenService = tokenService
            ?? throw new ArgumentNullException(nameof(tokenService));
    }

    public IssuedApiKey Execute(
        Guid requesterId,
        Guid oldKeyId,
        DateTimeOffset now)
    {
        if (requesterId == Guid.Empty)
        {
            throw new ArgumentException(
                "Requester ID cannot be empty.",
                nameof(requesterId));
        }

        if (oldKeyId == Guid.Empty)
        {
            throw new ArgumentException(
                "API key ID cannot be empty.",
                nameof(oldKeyId));
        }

        UserAccount? requester =
            _users.GetById(requesterId);

        if (requester is null ||
            !requester.IsActive ||
            requester.Role != UserRole.Admin)
        {
            throw new UnauthorizedAccessException(
                "Not authorized to rotate API keys.");
        }

        ApiKeyCredential? oldCredential =
            _credentials.GetById(oldKeyId);

        if (oldCredential is null)
        {
            throw new UnauthorizedAccessException(
                "Not authorized to rotate API keys.");
        }

        UserAccount? owner =
            _users.GetById(oldCredential.UserId);

        if (owner is null ||
            !owner.IsActive ||
            !string.Equals(
                requester.Keyspace,
                owner.Keyspace,
                StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Not authorized to rotate API keys.");
        }

        if (oldCredential.IsRevoked)
        {
            throw new InvalidOperationException(
                "API key is already revoked.");
        }

        if (now < oldCredential.CreatedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(now),
                "Rotation cannot precede key creation.");
        }

        var (replacement, plaintextKey) =
            _tokenService.Issue(owner.Id, now);

        _credentials.Rotate(
            oldCredential.Id,
            replacement,
            now);

        return new IssuedApiKey(
            replacement.Id,
            replacement.UserId,
            plaintextKey,
            replacement.CreatedAt);
    }
}