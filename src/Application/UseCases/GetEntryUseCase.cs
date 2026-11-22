using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Aggregates;

namespace KeyValueStore.Application.UseCases;

public class GetEntryUseCase
{
    private readonly IKeyValueEntryRepository _repository;

    public GetEntryUseCase(IKeyValueEntryRepository repository)
    {
        _repository = repository;
    }

    public KeyValueEntry? Execute(string keyspace, string key)
    {
        if (string.IsNullOrWhiteSpace(keyspace))
            throw new ArgumentException(
                "Keyspace cannot be empty.", nameof(keyspace));

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException(
                "Key cannot be empty.", nameof(key));

        return _repository.Get(keyspace, key);
    }
}