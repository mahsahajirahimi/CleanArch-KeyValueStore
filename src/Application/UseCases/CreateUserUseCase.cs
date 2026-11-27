using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Security;

namespace KeyValueStore.Application.UseCases;

public sealed record CreatedUser(
    Guid UserId,
    string Keyspace,
    UserRole Role);

public sealed class CreateUserUseCase
{
    private readonly IUserAccountRepository _users;

    public CreateUserUseCase(
        IUserAccountRepository users)
    {
        _users = users
            ?? throw new ArgumentNullException(nameof(users));
    }

    public CreatedUser Execute(
        Guid requesterId,
        UserRole role)
    {
        if (requesterId == Guid.Empty)
        {
            throw new ArgumentException(
                "Requester ID cannot be empty.",
                nameof(requesterId));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(
                nameof(role),
                "Invalid user role.");
        }

        UserAccount? requester =
            _users.GetById(requesterId);

        if (requester is null ||
            !requester.IsActive ||
            requester.Role != UserRole.Admin)
        {
            throw new UnauthorizedAccessException(
                "Only an active admin can create users.");
        }

        var user = new UserAccount(
            Guid.NewGuid(),
            requester.Keyspace,
            role);

        _users.Save(user);

        return new CreatedUser(
            user.Id,
            user.Keyspace,
            user.Role);
    }
}