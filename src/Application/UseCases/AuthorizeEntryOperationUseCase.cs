using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Security;

namespace KeyValueStore.Application.UseCases;

public sealed class AuthorizeEntryOperationUseCase
{
    private readonly IUserAccountRepository _users;

    public AuthorizeEntryOperationUseCase(
        IUserAccountRepository users)
    {
        _users = users
            ?? throw new ArgumentNullException(nameof(users));
    }

    public bool CanExecute(
        Guid requesterId,
        string requestedKeyspace,
        EntryOperation operation)
    {
        if (requesterId == Guid.Empty ||
            string.IsNullOrWhiteSpace(requestedKeyspace))
        {
            return false;
        }

        UserAccount? user = _users.GetById(requesterId);

        if (user is null)
            return false;

        return EntryAccessPolicy.IsAllowed(
            user,
            requestedKeyspace,
            operation);
    }

    public void EnsureAuthorized(
        Guid requesterId,
        string requestedKeyspace,
        EntryOperation operation)
    {
        if (!CanExecute(
            requesterId,
            requestedKeyspace,
            operation))
        {
            throw new UnauthorizedAccessException(
                "Not authorized to perform this entry operation.");
        }
    }
}