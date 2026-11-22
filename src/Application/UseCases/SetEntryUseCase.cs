using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Aggregates;

namespace KeyValueStore.Application.UseCases;

public class SetEntryUseCase
{
    private readonly IKeyValueEntryRepository _repository;

    public SetEntryUseCase(IKeyValueEntryRepository repository)
    {
        _repository = repository;
    }

    public void Execute(
        string keyspace,
        string key,
        string value,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(keyspace))
            throw new ArgumentException(
                "Keyspace cannot be empty.",
                nameof(keyspace));

        var entry = _repository.Get(keyspace, key);

        if (entry is null)
        {
            entry = new KeyValueEntry(key, value, now);
        }
        else
        {
            entry.UpdateValue(value, now);
        }

        _repository.Save(keyspace, entry);
    }
}