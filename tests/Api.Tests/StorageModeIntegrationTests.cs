using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Security;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KeyValueStore.Api.Tests;

public sealed class StorageModeIntegrationTests
{
    private const string Keyspace = "mode-test";

    private const string EntryUrl =
        "/api/keyspaces/mode-test/entries/test-entry";

    [Theory]
    [InlineData("InMemory")]
    [InlineData("Persistent")]
    public async Task BothModes_ShouldSupportCrudAndRbac(
        string storageMode)
    {
        using var factory = new TestApiFactory(
            storageMode: storageMode);

        string writerKey = SeedUser(
            factory,
            UserRole.Writer,
            Keyspace);

        string readerKey = SeedUser(
            factory,
            UserRole.Reader,
            Keyspace);

        using var writer = CreateClient(factory, writerKey);
        using var reader = CreateClient(factory, readerKey);
        using var anonymous = factory.CreateClient();

        using var put = await writer.PutAsJsonAsync(
            EntryUrl,
            new { value = "original" });

        Assert.Equal(
            HttpStatusCode.NoContent,
            put.StatusCode);

        using var writerRead = await writer.GetAsync(EntryUrl);

        Assert.Equal(
            HttpStatusCode.OK,
            writerRead.StatusCode);

        JsonElement body =
            await writerRead.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "original",
            body.GetProperty("value").GetString());

        using var readerRead = await reader.GetAsync(EntryUrl);

        Assert.Equal(
            HttpStatusCode.OK,
            readerRead.StatusCode);

        using var forbiddenWrite = await reader.PutAsJsonAsync(
            EntryUrl,
            new { value = "unauthorized-update" });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            forbiddenWrite.StatusCode);

        using var forbiddenDelete =
            await reader.DeleteAsync(EntryUrl);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            forbiddenDelete.StatusCode);

        using var foreignRead = await writer.GetAsync(
            "/api/keyspaces/another-team/entries/test-entry");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            foreignRead.StatusCode);

        using var anonymousRead =
            await anonymous.GetAsync(EntryUrl);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            anonymousRead.StatusCode);

        using var deleted = await writer.DeleteAsync(EntryUrl);

        Assert.Equal(
            HttpStatusCode.NoContent,
            deleted.StatusCode);

        using var missing = await writer.GetAsync(EntryUrl);

        Assert.Equal(
            HttpStatusCode.NotFound,
            missing.StatusCode);
    }

    [Theory]
    [InlineData("InMemory", false)]
    [InlineData("Persistent", true)]
    public async Task Restart_ShouldRespectStorageMode(
        string storageMode,
        bool shouldSurviveRestart)
    {
        string directory = NewDirectory();
        string apiKey = "";

        try
        {
            using (var firstFactory = new TestApiFactory(
                directory,
                storageMode: storageMode))
            {
                apiKey = SeedUser(
                    firstFactory,
                    UserRole.Writer,
                    Keyspace);

                using var firstClient =
                    CreateClient(firstFactory, apiKey);

                using var put = await firstClient.PutAsJsonAsync(
                    EntryUrl,
                    new { value = "persisted-value" });

                Assert.Equal(
                    HttpStatusCode.NoContent,
                    put.StatusCode);

                using var before =
                    await firstClient.GetAsync(EntryUrl);

                Assert.Equal(
                    HttpStatusCode.OK,
                    before.StatusCode);
            }

            using (var secondFactory = new TestApiFactory(
                directory,
                storageMode: storageMode))
            {
                using var secondClient =
                    CreateClient(secondFactory, apiKey);

                using var after =
                    await secondClient.GetAsync(EntryUrl);

                Assert.Equal(
                    shouldSurviveRestart
                        ? HttpStatusCode.OK
                        : HttpStatusCode.NotFound,
                    after.StatusCode);

                if (shouldSurviveRestart)
                {
                    JsonElement body =
                        await after.Content
                            .ReadFromJsonAsync<JsonElement>();

                    Assert.Equal(
                        "persisted-value",
                        body.GetProperty("value").GetString());
                }
            }
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task SeparateInMemoryApis_ShouldNotShareSecurityData()
    {
        using var firstFactory = new TestApiFactory(
            storageMode: "InMemory");

        using var secondFactory = new TestApiFactory(
            storageMode: "InMemory");

        using var firstAnonymous = firstFactory.CreateClient();
        using var secondAnonymous = secondFactory.CreateClient();

        string apiKey = SeedUser(
            firstFactory,
            UserRole.Writer,
            Keyspace);

        using var firstClient =
            CreateClient(firstFactory, apiKey);

        using var secondClient =
            CreateClient(secondFactory, apiKey);

        using var firstResponse =
            await firstClient.GetAsync(EntryUrl);

        Assert.Equal(
            HttpStatusCode.NotFound,
            firstResponse.StatusCode);

        using var secondResponse =
            await secondClient.GetAsync(EntryUrl);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            secondResponse.StatusCode);
    }

    [Theory]
    [InlineData(
        "Unknown",
        "4",
        null,
        "Unsupported storage mode")]
    [InlineData(
        "Persistent",
        "0",
        null,
        "Storage:CacheCapacity")]
    [InlineData(
        "Persistent",
        "4",
        " ",
        "Persistent storage requires a file path")]
    public void InvalidConfiguration_ShouldFailStartup(
        string storageMode,
        string cacheCapacity,
        string? storageFilePath,
        string expectedMessage)
    {
        using var factory = new TestApiFactory(
            storageMode: storageMode,
            cacheCapacity: cacheCapacity,
            storageFilePath: storageFilePath);

        var exception = Assert.ThrowsAny<Exception>(
            () => factory.CreateClient());

        Assert.Contains(
            expectedMessage,
            exception.ToString());
    }

    private static string SeedUser(
        TestApiFactory factory,
        UserRole role,
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
            role));

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

    private static string NewDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"KvsModeIntegration-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        return directory;
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(
                directory,
                recursive: true);
        }
    }
}