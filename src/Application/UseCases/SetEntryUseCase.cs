using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Security;

namespace KeyValueStore.Application.UseCases;

public class SetEntryUseCase
{
    private readonly IKeyValueEntryRepository _repository;
    private readonly AuthorizeEntryOperationUseCase _authorization;

    public SetEntryUseCase(
        IKeyValueEntryRepository repository,
        AuthorizeEntryOperationUseCase authorization)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));

        _authorization = authorization
            ?? throw new ArgumentNullException(nameof(authorization));
    }

    public void Execute(
        Guid requesterId,
        string keyspace,
        string key,
        string? value,
        DateTimeOffset now,
        int? ttlSeconds = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(keyspace))
        {
            throw new ArgumentException(
                "Keyspace cannot be empty.",
                nameof(keyspace));
        }

        _authorization.EnsureAuthorized(
            requesterId,
            keyspace,
            EntryOperation.Write);

        if (value is null)
        {
            throw new ArgumentException(
                "A value is required.",
                nameof(value));
        }

        if (ttlSeconds is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ttlSeconds),
                "TTL must be positive.");
        }

        if (metadata is not null)
        {
            if (metadata.Any(pair =>
                string.IsNullOrWhiteSpace(pair.Key)))
            {
                throw new ArgumentException(
                    "Metadata keys cannot be empty or whitespace.",
                    nameof(metadata));
            }

            if (metadata.Any(pair => pair.Value is null))
            {
                throw new ArgumentException(
                    "Metadata values cannot be null.",
                    nameof(metadata));
            }
        }

        DateTimeOffset? expiresAt = null;

        if (ttlSeconds.HasValue)
        {
            TimeSpan ttl =
                TimeSpan.FromSeconds(ttlSeconds.Value);

            expiresAt = now.Add(ttl);
        }

        _repository.Upsert(
            keyspace,
            key,
            value,
            now,
            expiresAt,
            metadata);
    }
}