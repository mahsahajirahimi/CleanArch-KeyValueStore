namespace KeyValueStore.Api.Contracts;

public sealed record IssueApiKeyResponse(
    Guid KeyId,
    Guid UserId,
    string ApiKey,
    DateTimeOffset CreatedAt);