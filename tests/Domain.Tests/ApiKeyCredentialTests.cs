using KeyValueStore.Domain.Security;
using Xunit;

namespace KeyValueStore.Domain.Tests;

public sealed class ApiKeyCredentialTests
{
    private const string ValidHash =
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public void Constructor_ShouldCreateActiveCredential()
    {
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var credential = new ApiKeyCredential(
            keyId, userId, ValidHash, now);

        Assert.Equal(keyId, credential.Id);
        Assert.Equal(userId, credential.UserId);
        Assert.Equal(ValidHash, credential.SecretHash);
        Assert.Equal(now, credential.CreatedAt);
        Assert.False(credential.IsRevoked);
        Assert.Null(credential.RevokedAt);
    }

    [Fact]
    public void Constructor_ShouldRejectEmptyIds()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() =>
            new ApiKeyCredential(
                Guid.Empty,
                Guid.NewGuid(),
                ValidHash,
                now));

        Assert.Throws<ArgumentException>(() =>
            new ApiKeyCredential(
                Guid.NewGuid(),
                Guid.Empty,
                ValidHash,
                now));
    }

    [Fact]
    public void Constructor_ShouldRejectInvalidHash()
    {
        Assert.Throws<ArgumentException>(() =>
            new ApiKeyCredential(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "invalid",
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Revoke_ShouldMarkCredentialAsRevoked()
    {
        var now = DateTimeOffset.UtcNow;

        var credential = new ApiKeyCredential(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ValidHash,
            now);

        credential.Revoke(now.AddMinutes(5));

        Assert.True(credential.IsRevoked);
        Assert.Equal(
            now.AddMinutes(5),
            credential.RevokedAt);
    }

    [Fact]
    public void Revoke_ShouldBeIdempotent()
    {
        var now = DateTimeOffset.UtcNow;

        var credential = new ApiKeyCredential(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ValidHash,
            now);

        credential.Revoke(now.AddMinutes(5));
        credential.Revoke(now.AddMinutes(10));

        Assert.Equal(
            now.AddMinutes(5),
            credential.RevokedAt);
    }

    [Fact]
    public void Revoke_ShouldRejectEarlierTimestamp()
    {
        var now = DateTimeOffset.UtcNow;

        var credential = new ApiKeyCredential(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ValidHash,
            now);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            credential.Revoke(now.AddMinutes(-1)));
    }
}