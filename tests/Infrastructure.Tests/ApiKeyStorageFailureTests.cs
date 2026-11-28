
using KeyValueStore.Infrastructure.Security;
using Xunit;

namespace KeyValueStore.Infrastructure.Tests;

public sealed class ApiKeyStorageFailureTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"KvsCredentialFailure-{Guid.NewGuid():N}");

    private string StorePath =>
        Path.Combine(_directory, "credentials.json");

    private readonly ApiKeyTokenService _tokens = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Save_FailureShouldPreserveExistingCredentials(
        bool failAtReplace)
    {
        var now = DateTimeOffset.UtcNow;

        var original = _tokens.Issue(
            Guid.NewGuid(),
            now.AddMinutes(-5));

        var additional = _tokens.Issue(
            Guid.NewGuid(),
            now);

        var initial =
            new JsonApiKeyCredentialRepository(StorePath);

        initial.Save(original.Credential);

        byte[] originalFile = File.ReadAllBytes(StorePath);

        var failing = CreateFailingRepository(
            failAtReplace);

        Assert.Throws<IOException>(() =>
            failing.Save(additional.Credential));

        AssertFileUnchanged(originalFile);

        var existing =
            failing.GetById(original.Credential.Id);

        Assert.NotNull(existing);
        Assert.True(_tokens.Verify(
            existing,
            original.PlaintextKey));

        Assert.Null(
            failing.GetById(additional.Credential.Id));

        var restarted =
            new JsonApiKeyCredentialRepository(StorePath);

        var restored =
            restarted.GetById(original.Credential.Id);

        Assert.NotNull(restored);
        Assert.True(_tokens.Verify(
            restored,
            original.PlaintextKey));

        Assert.Null(
            restarted.GetById(additional.Credential.Id));

        failing.Save(additional.Credential);

        var afterRetry =
            new JsonApiKeyCredentialRepository(StorePath);

        Assert.NotNull(
            afterRetry.GetById(original.Credential.Id));

        var savedAdditional =
            afterRetry.GetById(additional.Credential.Id);

        Assert.NotNull(savedAdditional);
        Assert.True(_tokens.Verify(
            savedAdditional,
            additional.PlaintextKey));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Revoke_FailureShouldLeaveKeyActive(
        bool failAtReplace)
    {
        var now = DateTimeOffset.UtcNow;

        var issued = _tokens.Issue(
            Guid.NewGuid(),
            now.AddMinutes(-5));

        var initial =
            new JsonApiKeyCredentialRepository(StorePath);

        initial.Save(issued.Credential);

        byte[] originalFile = File.ReadAllBytes(StorePath);

        var failing = CreateFailingRepository(
            failAtReplace);

        Assert.Throws<IOException>(() =>
            failing.Revoke(issued.Credential.Id, now));

        AssertFileUnchanged(originalFile);

        var current =
            failing.GetById(issued.Credential.Id);

        Assert.NotNull(current);
        Assert.False(current.IsRevoked);

        Assert.True(_tokens.Verify(
            current,
            issued.PlaintextKey));

        var restarted =
            new JsonApiKeyCredentialRepository(StorePath);

        var restored =
            restarted.GetById(issued.Credential.Id);

        Assert.NotNull(restored);
        Assert.False(restored.IsRevoked);

        Assert.True(_tokens.Verify(
            restored,
            issued.PlaintextKey));

        Assert.True(
            failing.Revoke(issued.Credential.Id, now));

        var afterRetry =
            new JsonApiKeyCredentialRepository(StorePath);

        var revoked =
            afterRetry.GetById(issued.Credential.Id);

        Assert.NotNull(revoked);
        Assert.True(revoked.IsRevoked);
        Assert.Equal(now, revoked.RevokedAt);

        Assert.False(_tokens.Verify(
            revoked,
            issued.PlaintextKey));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rotate_FailureShouldPreserveOldKeyAndRejectReplacement(
        bool failAtReplace)
    {
        var now = DateTimeOffset.UtcNow;
        Guid userId = Guid.NewGuid();

        var original = _tokens.Issue(
            userId,
            now.AddMinutes(-5));

        var replacement = _tokens.Issue(
            userId,
            now);

        var initial =
            new JsonApiKeyCredentialRepository(StorePath);

        initial.Save(original.Credential);

        byte[] originalFile = File.ReadAllBytes(StorePath);

        var failing = CreateFailingRepository(
            failAtReplace);

        Assert.Throws<IOException>(() =>
            failing.Rotate(
                original.Credential.Id,
                replacement.Credential,
                now));

        AssertFileUnchanged(originalFile);

        var currentOld =
            failing.GetById(original.Credential.Id);

        Assert.NotNull(currentOld);
        Assert.False(currentOld.IsRevoked);

        Assert.True(_tokens.Verify(
            currentOld,
            original.PlaintextKey));

        Assert.Null(
            failing.GetById(replacement.Credential.Id));

        var restarted =
            new JsonApiKeyCredentialRepository(StorePath);

        var restoredOld =
            restarted.GetById(original.Credential.Id);

        Assert.NotNull(restoredOld);
        Assert.False(restoredOld.IsRevoked);

        Assert.True(_tokens.Verify(
            restoredOld,
            original.PlaintextKey));

        Assert.Null(
            restarted.GetById(replacement.Credential.Id));

        failing.Rotate(
            original.Credential.Id,
            replacement.Credential,
            now);

        var afterRetry =
            new JsonApiKeyCredentialRepository(StorePath);

        var finalOld =
            afterRetry.GetById(original.Credential.Id);

        var finalNew =
            afterRetry.GetById(replacement.Credential.Id);

        Assert.NotNull(finalOld);
        Assert.NotNull(finalNew);

        Assert.True(finalOld.IsRevoked);
        Assert.Equal(now, finalOld.RevokedAt);

        Assert.False(finalNew.IsRevoked);

        Assert.False(_tokens.Verify(
            finalOld,
            original.PlaintextKey));

        Assert.True(_tokens.Verify(
            finalNew,
            replacement.PlaintextKey));
    }

    private JsonApiKeyCredentialRepository CreateFailingRepository(
        bool failAtReplace)
    {
        CredentialWriteStage target = failAtReplace
            ? CredentialWriteStage.BeforeReplace
            : CredentialWriteStage.BeforeTemporaryFileFlush;

        bool shouldFail = true;

        return new JsonApiKeyCredentialRepository(
            StorePath,
            stage =>
            {
                if (stage != target || !shouldFail)
                    return;

                shouldFail = false;

                throw new IOException(
                    $"Simulated storage failure at {stage}.");
            });
    }

    private void AssertFileUnchanged(byte[] originalFile)
    {
        Assert.True(
            originalFile.SequenceEqual(
                File.ReadAllBytes(StorePath)),
            "The original credential file was modified.");

        Assert.Empty(
            Directory.GetFiles(_directory, "*.tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(
                _directory,
                recursive: true);
        }
    }
}
