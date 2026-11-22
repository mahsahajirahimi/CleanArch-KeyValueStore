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
        DateTimeOffset now,
        int? ttlSeconds = null)
    {
        if (string.IsNullOrWhiteSpace(keyspace))
            throw new ArgumentException(
                "Keyspace cannot be empty.",
                nameof(keyspace));

        DateTimeOffset? expiresAt = null;

        if (ttlSeconds.HasValue)
        {
            if (ttlSeconds.Value <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(ttlSeconds),
                    "TTL must be positive.");

            TimeSpan ttl = TimeSpan.FromSeconds(ttlSeconds.Value);
            expiresAt = now.Add(ttl);
        }

        _repository.Upsert(
            keyspace,
            key,
            value,
            now,
            expiresAt);
    }
}