using KeyValueStore.Infrastructure.Security;
using Xunit;

namespace KeyValueStore.Infrastructure.Tests;

public sealed class ApiKeyTokenServiceTests
{
    private readonly ApiKeyTokenService _service = new();

    [Fact]
    public void Issue_ShouldGenerateVerifiableApiKey()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var issued = _service.Issue(userId, now);

        Assert.StartsWith("kvs_", issued.PlaintextKey);

        Assert.Equal(
            userId,
            issued.Credential.UserId);

        Assert.Equal(
            64,
            issued.Credential.SecretHash.Length);

        Assert.True(_service.Verify(
            issued.Credential,
            issued.PlaintextKey));
    }

    [Fact]
    public void Issue_ShouldGenerateDifferentKeysForSameUser()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var first = _service.Issue(userId, now);
        var second = _service.Issue(userId, now);

        Assert.NotEqual(
            first.Credential.Id,
            second.Credential.Id);

        Assert.NotEqual(
            first.PlaintextKey,
            second.PlaintextKey);

        Assert.NotEqual(
            first.Credential.SecretHash,
            second.Credential.SecretHash);

        Assert.True(_service.Verify(
            first.Credential,
            first.PlaintextKey));

        Assert.True(_service.Verify(
            second.Credential,
            second.PlaintextKey));
    }

    [Fact]
    public void Verify_ShouldRejectModifiedSecret()
    {
        var issued = _service.Issue(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        string original = issued.PlaintextKey;

        char replacement = original[^1] == 'A'
            ? 'B'
            : 'A';

        string modified = original[..^1] + replacement;

        Assert.False(_service.Verify(
            issued.Credential,
            modified));
    }

    [Fact]
    public void Verify_ShouldRejectDifferentCredential()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var first = _service.Issue(userId, now);
        var second = _service.Issue(userId, now);

        Assert.False(_service.Verify(
            first.Credential,
            second.PlaintextKey));
    }

    [Fact]
    public void Verify_ShouldRejectRevokedCredential()
    {
        var now = DateTimeOffset.UtcNow;

        var issued = _service.Issue(
            Guid.NewGuid(),
            now);

        issued.Credential.Revoke(now.AddMinutes(1));

        Assert.False(_service.Verify(
            issued.Credential,
            issued.PlaintextKey));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("kvs_")]
    [InlineData("wrong-prefix.secret")]
    public void Verify_ShouldRejectMalformedKey(string? key)
    {
        var issued = _service.Issue(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        Assert.False(_service.Verify(
            issued.Credential,
            key));
    }

    [Fact]
    public void Issue_ShouldRejectEmptyUserId()
    {
        Assert.Throws<ArgumentException>(() =>
            _service.Issue(
                Guid.Empty,
                DateTimeOffset.UtcNow));
    }
}