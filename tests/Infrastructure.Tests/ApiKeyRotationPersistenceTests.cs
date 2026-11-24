using KeyValueStore.Infrastructure.Security;
using Xunit;

namespace KeyValueStore.Infrastructure.Tests;

public sealed class ApiKeyRotationPersistenceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"ApiKeyRotation-{Guid.NewGuid():N}");

    private string StorePath =>
        Path.Combine(_directory, "credentials.json");

    private readonly ApiKeyTokenService _tokens = new();

    [Fact]
    public void Rotate_ShouldPersistBothChangesAfterRestart()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();

        var old = _tokens.Issue(
            userId, now.AddMinutes(-10));

        var replacement = _tokens.Issue(userId, now);

        var repository =
            new JsonApiKeyCredentialRepository(StorePath);

        repository.Save(old.Credential);

        repository.Rotate(
            old.Credential.Id,
            replacement.Credential,
            now);

        var restarted =
            new JsonApiKeyCredentialRepository(StorePath);

        var restoredOld =
            restarted.GetById(old.Credential.Id);

        var restoredNew =
            restarted.GetById(replacement.Credential.Id);

        Assert.NotNull(restoredOld);
        Assert.NotNull(restoredNew);

        Assert.True(restoredOld.IsRevoked);
        Assert.False(restoredNew.IsRevoked);

        Assert.False(_tokens.Verify(
            restoredOld, old.PlaintextKey));

        Assert.True(_tokens.Verify(
            restoredNew, replacement.PlaintextKey));
    }

    [Fact]
    public void Rotate_ShouldRejectDifferentUserWithoutChanges()
    {
        var now = DateTimeOffset.UtcNow;

        var old = _tokens.Issue(
            Guid.NewGuid(), now.AddMinutes(-10));

        var replacement = _tokens.Issue(
            Guid.NewGuid(), now);

        var repository =
            new JsonApiKeyCredentialRepository(StorePath);

        repository.Save(old.Credential);

        Assert.Throws<InvalidOperationException>(() =>
            repository.Rotate(
                old.Credential.Id,
                replacement.Credential,
                now));

        var restarted =
            new JsonApiKeyCredentialRepository(StorePath);

        Assert.False(
            restarted.GetById(old.Credential.Id)!.IsRevoked);

        Assert.Null(
            restarted.GetById(replacement.Credential.Id));
    }

    [Fact]
    public void Rotate_ShouldRejectAlreadyRevokedKey()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();

        var old = _tokens.Issue(
            userId, now.AddMinutes(-10));

        old.Credential.Revoke(now.AddMinutes(-5));

        var replacement = _tokens.Issue(userId, now);

        var repository =
            new JsonApiKeyCredentialRepository(StorePath);

        repository.Save(old.Credential);

        Assert.Throws<InvalidOperationException>(() =>
            repository.Rotate(
                old.Credential.Id,
                replacement.Credential,
                now));

        Assert.Null(
            repository.GetById(replacement.Credential.Id));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}