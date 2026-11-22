namespace KeyValueStore.Api.Contracts;

public sealed record SetEntryRequest(
    string? Value,
    int? TtlSeconds = null);