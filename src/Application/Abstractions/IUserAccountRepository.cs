using KeyValueStore.Domain.Security;

namespace KeyValueStore.Application.Abstractions;

public interface IUserAccountRepository
{
    UserAccount? GetById(Guid userId);
}