using System.Text.Json;
using KeyValueStore.Domain.Security;
using KeyValueStore.Infrastructure.Security;
using Xunit;

namespace KeyValueStore.Infrastructure.Tests;

public sealed class JsonApiKeyCredentialRepositoryTests
    : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"CredentialTests-{Guid.NewGuid():N}");

    private string StorePath =>
        Path.Combine(_directory, "credentials.json");

    private readonly ApiKeyTokenService _tokens = new();

    [Fact]
    public void GetById_ShouldReturnNullForUnknownKey()
    {
        var repository =
            new JsonApiKeyCredentialRepository(StorePath);

        Assert.Null(repository.GetById(Guid.NewGuid()));
    }

    [Fact]
    public void Save_ShouldPersistCredentialAfterRestart()
    {
        var issued = _tokens.Issue(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        var repository =
            new JsonApiKeyCredentialRepository(StorePath);

        repository.Save(issued.Credential);

        var restarted =
            new JsonApiKeyCredentialRepository(StorePath);

        var restored =
            restarted.GetById(issued.Credential.Id);

        Assert.NotNull(restored);

        Assert.True(_tokens.Verify(
            restored,
            issued.PlaintextKey));
    }

    [Fact]
    public void Save_ShouldPersistRevocation()
    {
        var now = DateTimeOffset.UtcNow;

        var issued = _tokens.Issue(
            Guid.NewGuid(), now);

        var repository =
            new JsonApiKeyCredentialRepository(StorePath);

        repository.Save(issued.Credential);

        var credential =
            repository.GetById(issued.Credential.Id)!;

        credential.Revoke(now.AddMinutes(1));
        repository.Save(credential);

        var restarted =
            new JsonApiKeyCredentialRepository(StorePath);

        var restored =
            restarted.GetById(credential.Id)!;

        Assert.True(restored.IsRevoked);

        Assert.False(_tokens.Verify(
            restored,
            issued.PlaintextKey));
    }

    [Fact]
    public void GetById_ShouldReturnDefensiveCopy()
    {
        var now = DateTimeOffset.UtcNow;

        var issued = _tokens.Issue(Guid.NewGuid(), now);

        var repository =
            new JsonApiKeyCredentialRepository(StorePath);

        repository.Save(issued.Credential);

        var copy =
            repository.GetById(issued.Credential.Id)!;

        copy.Revoke(now.AddMinutes(1));

        var original =
            repository.GetById(issued.Credential.Id)!;

        Assert.False(original.IsRevoked);
    }

    [Fact]
    public void Save_ShouldRejectReactivation()
    {
        var now = DateTimeOffset.UtcNow;

        var issued = _tokens.Issue(Guid.NewGuid(), now);

        var repository =
            new JsonApiKeyCredentialRepository(StorePath);

        issued.Credential.Revoke(now.AddMinutes(1));
        repository.Save(issued.Credential);

        var activeCopy = new ApiKeyCredential(
            issued.Credential.Id,
            issued.Credential.UserId,
            issued.Credential.SecretHash,
            issued.Credential.CreatedAt);

        Assert.Throws<InvalidOperationException>(() =>
        {
            repository.Save(activeCopy);
        });

        Assert.True(repository.GetById(
            issued.Credential.Id)!.IsRevoked);
    }

    [Fact]
    public void Save_ShouldNeverPersistPlaintextKey()
    {
        var issued = _tokens.Issue(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        var repository =
            new JsonApiKeyCredentialRepository(StorePath);

        repository.Save(issued.Credential);

        string content = File.ReadAllText(StorePath);

        Assert.DoesNotContain(
            issued.PlaintextKey,
            content);

        Assert.Contains(
            issued.Credential.SecretHash,
            content);
    }

    [Fact]
    public void Startup_ShouldRejectInvalidJson()
    {
        Directory.CreateDirectory(_directory);

        File.WriteAllText(
            StorePath,
            "{ invalid json");

        Assert.Throws<InvalidDataException>(() =>
        {
            _ = new JsonApiKeyCredentialRepository(
                StorePath);
        });
    }

    [Fact]
    public void Startup_ShouldRejectDuplicateIds()
    {
        var issued = _tokens.Issue(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        var repository =
            new JsonApiKeyCredentialRepository(StorePath);

        repository.Save(issued.Credential);

        using var document = JsonDocument.Parse(
            File.ReadAllText(StorePath));

        string item = document.RootElement[0].GetRawText();

        File.WriteAllText(
            StorePath,
            $"[{item},{item}]");

        Assert.Throws<InvalidDataException>(() =>
        {
            _ = new JsonApiKeyCredentialRepository(
                StorePath);
        });
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