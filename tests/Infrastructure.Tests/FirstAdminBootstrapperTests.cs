using KeyValueStore.Domain.Security;
using KeyValueStore.Infrastructure.Security;
using Xunit;

namespace KeyValueStore.Infrastructure.Tests;

public sealed class FirstAdminBootstrapperTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"BootstrapTests-{Guid.NewGuid():N}");

    private string UsersPath =>
        Path.Combine(_directory, "users.json");

    private string CredentialsPath =>
        Path.Combine(_directory, "credentials.json");

    [Fact]
    public void Execute_ShouldPersistFirstAdminAndApiKey()
    {
        var tokens = new ApiKeyTokenService();

        var bootstrapper = CreateBootstrapper();

        var result = bootstrapper.Execute(
            "userA",
            DateTimeOffset.UtcNow);

        var users = new JsonUserAccountRepository(UsersPath);
        var credentials =
            new JsonApiKeyCredentialRepository(CredentialsPath);

        var admin = users.GetById(result.UserId);
        var credential = credentials.GetById(result.KeyId);

        Assert.NotNull(admin);
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.Equal("userA", admin.Keyspace);
        Assert.True(admin.IsActive);

        Assert.NotNull(credential);
        Assert.Equal(admin.Id, credential.UserId);

        Assert.True(tokens.Verify(
            credential,
            result.PlaintextKey));
    }

    [Fact]
    public void Execute_ShouldRejectSecondBootstrap()
    {
        var bootstrapper = CreateBootstrapper();

        bootstrapper.Execute(
            "userA",
            DateTimeOffset.UtcNow);

        var restarted = CreateBootstrapper();

        Assert.Throws<InvalidOperationException>(() =>
            restarted.Execute(
                "userB",
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Execute_ShouldRejectEmptyKeyspace()
    {
        var bootstrapper = CreateBootstrapper();

        Assert.Throws<ArgumentException>(() =>
            bootstrapper.Execute(
                "",
                DateTimeOffset.UtcNow));

        Assert.False(
            new JsonUserAccountRepository(UsersPath).HasAny());
    }

    private FirstAdminBootstrapper CreateBootstrapper()
    {
        return new FirstAdminBootstrapper(
            new JsonUserAccountRepository(UsersPath),
            new JsonApiKeyCredentialRepository(CredentialsPath),
            new ApiKeyTokenService());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}