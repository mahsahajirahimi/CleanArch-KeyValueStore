namespace KeyValueStore.Api.Contracts;

public sealed record CreateUserResponse(
    Guid UserId,
    string Keyspace,
    string Role);
