
namespace KeyValueStore.Api.Contracts;

public sealed record EntryResponse(
    string Key,
    string Value,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ExpiresAt);
