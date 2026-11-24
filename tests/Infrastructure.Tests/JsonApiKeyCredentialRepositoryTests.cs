using KeyValueStore.Domain.Security;
using KeyValueStore.Infrastructure.Security;
using Xunit;

namespace KeyValueStore.Infrastructure.Tests;

public sealed class JsonApiKeyCredentialRepositoryTests
    : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"KeyValueStoreCredentialTests-{Guid.NewGuid():N}");

    private string StorePath =>
        Path.Combine(_directory, "credentials.json");

    private readonly ApiKeyTokenService _tokenService = new();

    [Fact]
    public void Save_ShouldRestoreCredentialAfterRestart()
    {
        var userId = Guid.NewGuid();
        var issued = _tokenService.Issue(
            userId,
            DateTimeOffset.UtcNow);

        var repository =
            new JsonApiKeyCredentialRepository(StorePath);

        repository.Save(issued.Credential);

        var restarted =
            new JsonApiKeyCredentialRepository(StorePath);

        var restored = restarted.GetById(
            issued.Credential.Id);

        Assert.NotNull(restored);
        Assert.Equal(userId, restored.UserId);

        Assert.True(_tokenService.Verify(
            restored,
            issued.PlaintextKey));
    }

    [Fact]
    public void Save_ShouldPersistRevocationAfterRestart()
    {
        var now = DateTimeOffset.UtcNow;

        var issued = _tokenService.Issue(
            Guid.NewGuid(),
            now);

        var repository =
            new JsonApiKeyCredentialRepository(StorePath);

        repository.Save(issued.Credential);

        var credential = repository.GetById(
            issued.Credential.Id);

        Assert.NotNull(credential);

        credential.Revoke(now.AddMinutes(1));
        repository.Save(credential);

        var restarted =
            new JsonApiKeyCredentialRepository(StorePath);

        var restored = restarted.GetById(
            issued.Credential.Id);

        Assert.NotNull(restored);
        Assert.True(restored.IsRevoked);

        Assert.False(_tokenService.Verify(
            restored,
            issued.PlaintextKey));
    }

    [Fact]
    public void GetByUserId_ShouldReturnOnlyUsersCredentials()
    {
        var repository =
            new JsonApiKeyCredentialRepository(StorePath);

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var first = _tokenService.Issue(userA, now);
        var second = _tokenService.Issue(userA, now);
        var third = _tokenService.Issue(userB, now);

        repository.Save(first.Credential);
        repository.Save(second.Credential);
        repository.Save(third.Credential);

        var restarted =
            new JsonApiKeyCredentialRepository(StorePath);

        var userAKeys = restarted.GetByUserId(userA);

        Assert.Equal(2, userAKeys.Count);
        Assert.All(
            userAKeys,
            credential => Assert.Equal(
                userA,
                credential.UserId));
    }

    [Fact]
    public void GetById_ShouldReturnDefensiveCopy()
    {
        var now = DateTimeOffset.UtcNow;

        var issued = _tokenService.Issue(
            Guid.NewGuid(),
            now);

        var repository =
            new JsonApiKeyCredentialRepository(StorePath);

        repository.Save(issued.Credential);

        var first = repository.GetById(
            issued.Credential.Id);

        Assert.NotNull(first);

        first.Revoke(now.AddMinutes(1));

        var second = repository.GetById(
            issued.Credential.Id);

        Assert.NotNull(second);
        Assert.False(second.IsRevoked);
    }

    [Fact]
    public void Save_ShouldRejectReactivation()
    {
        var now = DateTimeOffset.UtcNow;

        var issued = _tokenService.Issue(
            Guid.NewGuid(),
            now);

        var repository =
            new JsonApiKeyCredentialRepository(StorePath);

        issued.Credential.Revoke(now.AddMinutes(1));
        repository.Save(issued.Credential);

        var oldActiveVersion = new ApiKeyCredential(
            issued.Credential.Id,
            issued.Credential.UserId,
            issued.Credential.SecretHash,
            issued.Credential.CreatedAt);

        Assert.Throws<InvalidOperationException>(() =>
            repository.Save(oldActiveVersion));

        Assert.True(repository.GetById(
            issued.Credential.Id)!.IsRevoked);
    }

    [Fact]
    public void Save_ShouldNotPersistPlaintextKey()
    {
        var issued = _tokenService.Issue(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        var repository =
            new JsonApiKeyCredentialRepository(StorePath);

        repository.Save(issued.Credential);

        string fileContent = File.ReadAllText(StorePath);

        Assert.DoesNotContain(
            issued.PlaintextKey,
            fileContent);

        Assert.Contains(
            issued.Credential.SecretHash,
            fileContent);
    }

    [Fact]
    public void Startup_ShouldRejectCorruptedCredentialFile()
    {
        Directory.CreateDirectory(_directory);

        File.WriteAllText(
            StorePath,
            "{invalid json");

        string original = File.ReadAllText(StorePath);

        Assert.Throws<InvalidDataException>(() =>
            new JsonApiKeyCredentialRepository(StorePath));

        Assert.Equal(
            original,
            File.ReadAllText(StorePath));
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