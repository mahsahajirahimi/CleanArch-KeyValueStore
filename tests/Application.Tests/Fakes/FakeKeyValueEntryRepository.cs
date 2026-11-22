using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Aggregates;

namespace KeyValueStore.Application.Tests.Fakes;

public class FakeKeyValueEntryRepository : IKeyValueEntryRepository
{
    private readonly Dictionary<(string Keyspace, string Key), KeyValueEntry>
        _entries = new();

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
        DateTimeOffset now)
    {
        var entry = Get(keyspace, key);

        if (entry is null || entry.IsExpired(now))
        {
            Save(
                keyspace,
                new KeyValueEntry(key, value, now));

            return;
        }

        var updateTime = now < entry.UpdatedAt
            ? entry.UpdatedAt
            : now;

        entry.UpdateValue(value, updateTime);
        Save(keyspace, entry);
    }
}