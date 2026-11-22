using KeyValueStore.Application.Abstractions;

namespace KeyValueStore.Application.UseCases;

public class DeleteEntryUseCase
{
    private readonly IKeyValueEntryRepository _repository;

    public DeleteEntryUseCase(IKeyValueEntryRepository repository)
    {
        _repository = repository;
    }

    public bool Execute(string keyspace, string key)
    {
        if (string.IsNullOrWhiteSpace(keyspace))
            throw new ArgumentException(
                "Keyspace cannot be empty.", nameof(keyspace));

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException(
                "Key cannot be empty.", nameof(key));

        return _repository.Delete(keyspace, key);
    }
}