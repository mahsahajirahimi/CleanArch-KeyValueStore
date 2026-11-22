using KeyValueStore.Application.Abstractions;

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

        _repository.Upsert(keyspace, key, value, now);
    }
}