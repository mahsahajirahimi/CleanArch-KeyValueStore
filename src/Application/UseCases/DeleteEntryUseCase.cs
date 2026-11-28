using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Security;

namespace KeyValueStore.Application.UseCases;

public class DeleteEntryUseCase
{
    private readonly IKeyValueEntryRepository _repository;
    private readonly AuthorizeEntryOperationUseCase _authorization;

    public DeleteEntryUseCase(
        IKeyValueEntryRepository repository,
        AuthorizeEntryOperationUseCase authorization)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));

        _authorization = authorization
            ?? throw new ArgumentNullException(nameof(authorization));
    }

    public bool Execute(
        Guid requesterId,
        string keyspace,
        string key)
    {
        if (string.IsNullOrWhiteSpace(keyspace))
        {
            throw new ArgumentException(
                "Keyspace cannot be empty.",
                nameof(keyspace));
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException(
                "Key cannot be empty.",
                nameof(key));
        }

        _authorization.EnsureAuthorized(
            requesterId,
            keyspace,
            EntryOperation.Delete);

        return _repository.Delete(keyspace, key);
    }
}