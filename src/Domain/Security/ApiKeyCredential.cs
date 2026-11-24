namespace KeyValueStore.Domain.Security;

public sealed class ApiKeyCredential
{
    public Guid Id { get; }
    public Guid UserId { get; }
    public string SecretHash { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsRevoked => RevokedAt.HasValue;

    public ApiKeyCredential(
        Guid id,
        Guid userId,
        string secretHash,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException(
                "API key ID cannot be empty.",
                nameof(id));

        if (userId == Guid.Empty)
            throw new ArgumentException(
                "User ID cannot be empty.",
                nameof(userId));

        if (string.IsNullOrWhiteSpace(secretHash) ||
            secretHash.Length != 64 ||
            !secretHash.All(Uri.IsHexDigit))
        {
            throw new ArgumentException(
                "Secret hash must be a 64-character SHA-256 hex string.",
                nameof(secretHash));
        }

        Id = id;
        UserId = userId;
        SecretHash = secretHash.ToUpperInvariant();
        CreatedAt = createdAt;
    }

    public void Revoke(DateTimeOffset revokedAt)
    {
        if (IsRevoked)
            return;

        if (revokedAt < CreatedAt)
            throw new ArgumentOutOfRangeException(
                nameof(revokedAt),
                "Revocation cannot precede creation.");

        RevokedAt = revokedAt;
    }
}