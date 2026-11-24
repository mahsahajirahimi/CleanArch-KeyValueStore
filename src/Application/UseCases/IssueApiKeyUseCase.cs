using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Security;

namespace KeyValueStore.Application.UseCases;

public sealed record IssuedApiKey(
    Guid KeyId,
    Guid UserId,
    string PlaintextKey,
    DateTimeOffset CreatedAt);

public sealed class IssueApiKeyUseCase
{
    private readonly IUserAccountRepository _users;
    private readonly IApiKeyCredentialRepository _credentials;
    private readonly IApiKeyTokenService _tokenService;

    public IssueApiKeyUseCase(
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
        Guid targetUserId,
        DateTimeOffset now)
    {
        if (requesterId == Guid.Empty)
            throw new ArgumentException(
                "Requester ID cannot be empty.",
                nameof(requesterId));

        if (targetUserId == Guid.Empty)
            throw new ArgumentException(
                "Target user ID cannot be empty.",
                nameof(targetUserId));

        UserAccount? requester =
            _users.GetById(requesterId);

        if (requester is null ||
            !requester.IsActive ||
            requester.Role != UserRole.Admin)
        {
            throw new UnauthorizedAccessException(
                "Not authorized to issue API keys.");
        }

        UserAccount? target =
            _users.GetById(targetUserId);

        if (target is null ||
            !target.IsActive ||
            !string.Equals(
                requester.Keyspace,
                target.Keyspace,
                StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Not authorized to issue API keys.");
        }

        var (credential, plaintextKey) =
            _tokenService.Issue(target.Id, now);

        _credentials.Save(credential);

        return new IssuedApiKey(
            credential.Id,
            target.Id,
            plaintextKey,
            credential.CreatedAt);
    }
}