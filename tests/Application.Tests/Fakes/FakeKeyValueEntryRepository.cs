using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Aggregates;

namespace KeyValueStore.Application.Tests.Fakes;

public class FakeKeyValueEntryRepository : IKeyValueEntryRepository
{
    private readonly Dictionary<(string, string), KeyValueEntry> _entries = new();

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
}