using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KeyValueStore.Domain.Security;
using KeyValueStore.Infrastructure.Security;
using Xunit;

namespace KeyValueStore.Api.Tests;

public sealed class KeyspaceProvisioningTests
{
    [Fact]
    public void Provision_ShouldPreserveExistingUsers_AndRejectDuplicates()
    {
        string dir = NewDirectory();

        try
        {
            var bootstrapper = NewBootstrapper(dir);
            var tokens = new ApiKeyTokenService();

            var first = bootstrapper.Execute(
                "team-a",
                DateTimeOffset.UtcNow);

            var second = bootstrapper.ProvisionAdditionalKeyspace(
                "team-b",
                DateTimeOffset.UtcNow);

            Assert.NotEqual(first.UserId, second.UserId);
            Assert.NotEqual(first.KeyId, second.KeyId);

            string usersPath = Path.Combine(dir, "users.json");
            string keysPath = Path.Combine(dir, "credentials.json");

            byte[] usersBefore = File.ReadAllBytes(usersPath);
            byte[] keysBefore = File.ReadAllBytes(keysPath);

            Assert.Throws<InvalidOperationException>(() =>
                bootstrapper.ProvisionAdditionalKeyspace(
                    "team-b",
                    DateTimeOffset.UtcNow));

            Assert.True(usersBefore.SequenceEqual(
                File.ReadAllBytes(usersPath)));

            Assert.True(keysBefore.SequenceEqual(
                File.ReadAllBytes(keysPath)));

            var users = new JsonUserAccountRepository(usersPath);
            var keys = new JsonApiKeyCredentialRepository(keysPath);

            Assert.Equal(
                "team-a",
                users.GetById(first.UserId)!.Keyspace);

            Assert.Equal(
                "team-b",
                users.GetById(second.UserId)!.Keyspace);

            Assert.Equal(
                UserRole.Admin,
                users.GetById(second.UserId)!.Role);

            Assert.True(tokens.Verify(
                keys.GetById(first.KeyId)!,
                first.PlaintextKey));

            Assert.True(tokens.Verify(
                keys.GetById(second.KeyId)!,
                second.PlaintextKey));
        }
        finally
        {
            DeleteDirectory(dir);
        }
    }

    [Fact]
    public void Provision_RequiresInitialBootstrap()
    {
        string dir = NewDirectory();

        try
        {
            var bootstrapper = NewBootstrapper(dir);

            Assert.Throws<InvalidOperationException>(() =>
                bootstrapper.ProvisionAdditionalKeyspace(
                    "team-b",
                    DateTimeOffset.UtcNow));
        }
        finally
        {
            DeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task TwoProvisionedKeyspaces_ShouldStayIsolatedThroughHttp()
    {
        string dir = NewDirectory();

        try
        {
            var bootstrapper = NewBootstrapper(dir);

            var adminA = bootstrapper.Execute(
                "team-a",
                DateTimeOffset.UtcNow);

            var adminB = bootstrapper.ProvisionAdditionalKeyspace(
                "team-b",
                DateTimeOffset.UtcNow);

            using var factory = new TestApiFactory(dir);

            using var clientA = factory.CreateClient();
            using var clientB = factory.CreateClient();

            clientA.DefaultRequestHeaders.Add(
                "X-API-Key", adminA.PlaintextKey);

            clientB.DefaultRequestHeaders.Add(
                "X-API-Key", adminB.PlaintextKey);

            const string urlA =
                "/api/keyspaces/team-a/entries/shared-name";

            const string urlB =
                "/api/keyspaces/team-b/entries/shared-name";

            using var writeA = await clientA.PutAsJsonAsync(
                urlA, new { value = "value-a" });

            using var writeB = await clientB.PutAsJsonAsync(
                urlB, new { value = "value-b" });

            Assert.Equal(
                HttpStatusCode.NoContent, writeA.StatusCode);

            Assert.Equal(
                HttpStatusCode.NoContent, writeB.StatusCode);

            using var readA = await clientA.GetAsync(urlA);
            using var readB = await clientB.GetAsync(urlB);

            Assert.Equal(HttpStatusCode.OK, readA.StatusCode);
            Assert.Equal(HttpStatusCode.OK, readB.StatusCode);

            JsonElement bodyA =
                await readA.Content.ReadFromJsonAsync<JsonElement>();

            JsonElement bodyB =
                await readB.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(
                "value-a",
                bodyA.GetProperty("value").GetString());

            Assert.Equal(
                "value-b",
                bodyB.GetProperty("value").GetString());

            using var forbiddenA = await clientA.GetAsync(urlB);
            using var forbiddenB = await clientB.GetAsync(urlA);

            Assert.Equal(
                HttpStatusCode.Forbidden,
                forbiddenA.StatusCode);

            Assert.Equal(
                HttpStatusCode.Forbidden,
                forbiddenB.StatusCode);
        }
        finally
        {
            DeleteDirectory(dir);
        }
    }

    private static FirstAdminBootstrapper NewBootstrapper(
        string directory)
    {
        return new FirstAdminBootstrapper(
            new JsonUserAccountRepository(
                Path.Combine(directory, "users.json")),
            new JsonApiKeyCredentialRepository(
                Path.Combine(directory, "credentials.json")),
            new ApiKeyTokenService());
    }

    private static string NewDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"KvsProvisionTests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}