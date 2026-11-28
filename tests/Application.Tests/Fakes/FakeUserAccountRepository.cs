using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Security;

namespace KeyValueStore.Application.Tests.Fakes;

public sealed class FakeUserAccountRepository
    : IUserAccountRepository
{
    private readonly Dictionary<Guid, UserAccount> _users = new();

    public UserAccount? GetById(Guid userId)
    {
        return _users.GetValueOrDefault(userId);
    }

    public void Save(UserAccount user)
    {
        ArgumentNullException.ThrowIfNull(user);

        _users[user.Id] = user;
    }
}