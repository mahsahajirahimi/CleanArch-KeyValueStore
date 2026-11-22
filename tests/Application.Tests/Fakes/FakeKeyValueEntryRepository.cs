using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Aggregates;

namespace KeyValueStore.Application.Tests.Fakes;

public class FakeKeyValueEntryRepository : IKeyValueEntryRepository
{
    private readonly Dictionary<(string Keyspace, string Key), KeyValueEntry>
        _entries = new();

    public (
        string Keyspace,
        string Key,
        string Value,
        DateTimeOffset Now,
        DateTimeOffset? ExpiresAt
    )? LastUpsert { get; private set; }

    public KeyValueEntry? Get(string keyspace, string key)
    {
        return _entries.GetValueOrDefault((keyspace, key));
    }

    public void Save(string keyspace, KeyValueEntry entry)
    {
        _entries[(keyspace, entry.Key)] = entry;
    }

    public bool Delete(string keyspace, string key)
    {
        return _entries.Remove((keyspace, key));
    }

    public void Upsert(
        string keyspace,
        string key,
        string value,
        DateTimeOffset now,
        DateTimeOffset? expiresAt = null)
    {
        LastUpsert = (keyspace, key, value, now, expiresAt);
    }
}