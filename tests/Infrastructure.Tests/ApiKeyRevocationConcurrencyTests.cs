using KeyValueStore.Infrastructure.Security;
using Xunit;

namespace KeyValueStore.Infrastructure.Tests;

public sealed class ApiKeyRevocationConcurrencyTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"KvsRevokeConcurrency-{Guid.NewGuid():N}");

    private string StorePath =>
        Path.Combine(_directory, "credentials.json");

    private readonly ApiKeyTokenService _tokens = new();

    [Fact]
    public async Task ConcurrentRevoke_ShouldRevokeExactlyOnce()
    {
        var now = DateTimeOffset.UtcNow;

        var issued = _tokens.Issue(
            Guid.NewGuid(),
            now.AddMinutes(-5));

        var repository =
            new JsonApiKeyCredentialRepository(StorePath);

        repository.Save(issued.Credential);

        Task<bool>[] operations = Enumerable
            .Range(0, 8)
            .Select(_ => Task.Run(() =>
                repository.Revoke(issued.Credential.Id, now)))
            .ToArray();

        bool[] results = await Task.WhenAll(operations);

        Assert.Single(results, result => result);

        var restarted =
            new JsonApiKeyCredentialRepository(StorePath);

        var stored = restarted.GetById(issued.Credential.Id);

        Assert.NotNull(stored);
        Assert.True(stored.IsRevoked);
        Assert.Equal(now, stored.RevokedAt);

        Assert.False(_tokens.Verify(
            stored,
            issued.PlaintextKey));
    }

    [Fact]
    public async Task ConcurrentRevokeAndRotate_ShouldRemainConsistent()
    {
        var now = DateTimeOffset.UtcNow;
        Guid userId = Guid.NewGuid();

        var original = _tokens.Issue(
            userId,
            now.AddMinutes(-5));

        var replacement = _tokens.Issue(userId, now);

        var repository =
            new JsonApiKeyCredentialRepository(StorePath);

        repository.Save(original.Credential);

        Task<bool> revokeTask = Task.Run(() =>
            repository.Revoke(original.Credential.Id, now));

        Task<bool> rotateTask = Task.Run(() =>
        {
            try
            {
                repository.Rotate(
                    original.Credential.Id,
                    replacement.Credential,
                    now);

                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        });

        await Task.WhenAll(revokeTask, rotateTask);

        bool revokeSucceeded = await revokeTask;
        bool rotateSucceeded = await rotateTask;

        Assert.NotEqual(revokeSucceeded, rotateSucceeded);

        var restarted =
            new JsonApiKeyCredentialRepository(StorePath);

        var oldKey = restarted.GetById(original.Credential.Id);
        var newKey = restarted.GetById(replacement.Credential.Id);

        Assert.NotNull(oldKey);
        Assert.True(oldKey.IsRevoked);

        if (rotateSucceeded)
        {
            Assert.NotNull(newKey);
            Assert.False(newKey.IsRevoked);
            Assert.True(_tokens.Verify(
                newKey,
                replacement.PlaintextKey));
        }
        else
        {
            Assert.Null(newKey);
        }

        Assert.False(_tokens.Verify(
            oldKey,
            original.PlaintextKey));
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