namespace KeyValueStore.Domain.Aggregates;

public class KeyValueEntry
{
    public string Key { get; }
    public string Value { get; private set; }

    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; }

    public KeyValueEntry(
        string key,
        string value,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty.", nameof(key));

        Key = key;
        Value = value ?? throw new ArgumentNullException(nameof(value));

        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public void UpdateValue(string newValue, DateTimeOffset updatedAt)
    {
        if (updatedAt < UpdatedAt)
            throw new ArgumentOutOfRangeException(nameof(updatedAt));

        Value = newValue ?? throw new ArgumentNullException(nameof(newValue));
        UpdatedAt = updatedAt;
    }

    public bool IsExpired(DateTimeOffset now)
    {
        return ExpiresAt.HasValue && now >= ExpiresAt.Value;
    }
}