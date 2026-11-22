using System.Collections.ObjectModel;

namespace KeyValueStore.Domain.Aggregates;

public class KeyValueEntry
{
    public string Key { get; }
    public string Value { get; private set; }

    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }

    public IReadOnlyDictionary<string, string> Metadata { get; private set; }
        = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>());

    public KeyValueEntry(
        string key,
        string value,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException(
                "Key cannot be empty.", nameof(key));

        Key = key;
        Value = value ?? throw new ArgumentNullException(nameof(value));

        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        ExpiresAt = expiresAt;

        UpdateMetadata(metadata);
    }

    public void UpdateValue(string newValue, DateTimeOffset updatedAt)
    {
        if (updatedAt < UpdatedAt)
            throw new ArgumentOutOfRangeException(nameof(updatedAt));

        Value = newValue
            ?? throw new ArgumentNullException(nameof(newValue));

        UpdatedAt = updatedAt;
    }

    public void UpdateExpiration(DateTimeOffset? expiresAt)
    {
        ExpiresAt = expiresAt;
    }

    public void UpdateMetadata(
        IReadOnlyDictionary<string, string>? metadata)
    {
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);

        if (metadata is not null)
        {
            foreach (var pair in metadata)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    throw new ArgumentException(
                        "Metadata key cannot be empty.",
                        nameof(metadata));

                if (pair.Value is null)
                    throw new ArgumentException(
                        "Metadata value cannot be null.",
                        nameof(metadata));

                copy.Add(pair.Key, pair.Value);
            }
        }

        Metadata = new ReadOnlyDictionary<string, string>(copy);
    }

    public bool IsExpired(DateTimeOffset now)
    {
        return ExpiresAt.HasValue && now >= ExpiresAt.Value;
    }
}