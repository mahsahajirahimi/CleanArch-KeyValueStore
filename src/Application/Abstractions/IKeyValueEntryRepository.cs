using KeyValueStore.Domain.Aggregates;

namespace KeyValueStore.Application.Abstractions;

public interface IKeyValueEntryRepository
{
    KeyValueEntry? Get(string keyspace, string key);

    void Save(string keyspace, KeyValueEntry entry);

    bool Delete(string keyspace, string key);

    void Upsert(
        string keyspace,
        string key,
        string value,
        DateTimeOffset now,
        DateTimeOffset? expiresAt = null);
}