using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Security;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KeyValueStore.Api.Tests;

public sealed class PersistentEntryTests
{
    [Fact]
    public async Task Entry_WithTtl_ShouldExpire()
    {
        using var factory = new TestApiFactory();

        string apiKey = SeedWriter(factory, "ttl-space");
        using var client = CreateClient(factory, apiKey);

        const string url =
            "/api/keyspaces/ttl-space/entries/temporary";

        using var put = await client.PutAsJsonAsync(
            url,
            new
            {
                value = "temporary-value",
                ttlSeconds = 2
            });

        Assert.Equal(
            HttpStatusCode.NoContent,
            put.StatusCode);

        using var before = await client.GetAsync(url);

        Assert.Equal(
            HttpStatusCode.OK,
            before.StatusCode);

        await Task.Delay(TimeSpan.FromSeconds(3));

        using var after = await client.GetAsync(url);

        Assert.Equal(
            HttpStatusCode.NotFound,
            after.StatusCode);
    }

    [Fact]
    public async Task ValueAndMetadata_ShouldSurviveRestart()
    {
        string sharedDirectory = CreateSharedDirectory();
        string apiKey;

        try
        {
            using (var firstFactory =
                   new TestApiFactory(sharedDirectory))
            {
                apiKey = SeedWriter(
                    firstFactory, "restart-space");

                using var client =
                    CreateClient(firstFactory, apiKey);

                const string url =
                    "/api/keyspaces/restart-space/entries/persisted";

                using var put = await client.PutAsJsonAsync(
                    url,
                    new
                    {
                        value = "survives-restart",
                        ttlSeconds = 3600,
                        metadata = new Dictionary<string, string>
                        {
                            ["source"] = "integration-test",
                            ["version"] = "1"
                        }
                    });

                Assert.Equal(
                    HttpStatusCode.NoContent,
                    put.StatusCode);

                using var before = await client.GetAsync(url);

                Assert.Equal(
                    HttpStatusCode.OK,
                    before.StatusCode);
            }

            using (var secondFactory =
                   new TestApiFactory(sharedDirectory))
            {
                using var client =
                    CreateClient(secondFactory, apiKey);

                const string url =
                    "/api/keyspaces/restart-space/entries/persisted";

                using var response =
                    await client.GetAsync(url);

                Assert.Equal(
                    HttpStatusCode.OK,
                    response.StatusCode);

                JsonElement body =
                    await response.Content
                        .ReadFromJsonAsync<JsonElement>();

                Assert.Equal(
                    "survives-restart",
                    body.GetProperty("value").GetString());

                JsonElement metadata =
                    body.GetProperty("metadata");

                Assert.Equal(
                    "integration-test",
                    metadata.GetProperty("source").GetString());

                Assert.Equal(
                    "1",
                    metadata.GetProperty("version").GetString());

                Assert.NotEqual(
                    JsonValueKind.Null,
                    body.GetProperty("expiresAt").ValueKind);
            }
        }
        finally
        {
            DeleteSharedDirectory(sharedDirectory);
        }
    }

    [Fact]
    public async Task ExpiredEntry_ShouldNotReappearAfterRestart()
    {
        string sharedDirectory = CreateSharedDirectory();
        string apiKey;

        try
        {
            using (var firstFactory =
                   new TestApiFactory(sharedDirectory))
            {
                apiKey = SeedWriter(
                    firstFactory, "expiry-space");

                using var client =
                    CreateClient(firstFactory, apiKey);

                using var put = await client.PutAsJsonAsync(
                    "/api/keyspaces/expiry-space/entries/short-lived",
                    new
                    {
                        value = "must-expire",
                        ttlSeconds = 2
                    });

                Assert.Equal(
                    HttpStatusCode.NoContent,
                    put.StatusCode);
            }

            await Task.Delay(TimeSpan.FromSeconds(3));

            using (var secondFactory =
                   new TestApiFactory(sharedDirectory))
            {
                using var client =
                    CreateClient(secondFactory, apiKey);

                using var response = await client.GetAsync(
                    "/api/keyspaces/expiry-space/entries/short-lived");

                Assert.Equal(
                    HttpStatusCode.NotFound,
                    response.StatusCode);
            }
        }
        finally
        {
            DeleteSharedDirectory(sharedDirectory);
        }
    }

    private static string SeedWriter(
        TestApiFactory factory,
        string keyspace)
    {
        using var scope = factory.Services.CreateScope();

        var users = scope.ServiceProvider
            .GetRequiredService<IUserAccountRepository>();

        var credentials = scope.ServiceProvider
            .GetRequiredService<IApiKeyCredentialRepository>();

        var tokens = scope.ServiceProvider
            .GetRequiredService<IApiKeyTokenService>();

        Guid userId = Guid.NewGuid();

        users.Save(new UserAccount(
            userId,
            keyspace,
            UserRole.Writer));

        var (credential, plaintextKey) = tokens.Issue(
            userId,
            DateTimeOffset.UtcNow);

        credentials.Save(credential);

        return plaintextKey;
    }

    private static HttpClient CreateClient(
        TestApiFactory factory,
        string apiKey)
    {
        var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-API-Key",
            apiKey);

        return client;
    }

    private static string CreateSharedDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"KeyValueStoreRestartTests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        return directory;
    }

    private static void DeleteSharedDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(
                directory,
                recursive: true);
        }
    }
}