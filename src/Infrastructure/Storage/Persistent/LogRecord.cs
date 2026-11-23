namespace KeyValueStore.Infrastructure.Storage.Persistent;

internal enum LogOperation
{
    Set,
    Delete
}

internal sealed record LogRecord(
    LogOperation Operation,
    string Keyspace,
    string Key,
    StoredEntry? Entry);
