
namespace KeyValueStore.Infrastructure.Storage.Persistent;

internal sealed record StoredEntry(
    string Keyspace,
    string Key,
    string Value,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ExpiresAt,
    Dictionary<string, string> Metadata);
