using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Security;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KeyValueStore.Api.Tests;

public sealed class EntryValidationTests
{
    private const string Url =
        "/api/keyspaces/validation-space/entries/test-key";

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Put_WithInvalidTtl_ShouldReturn400(
        int ttlSeconds)
    {
        using var factory = new TestApiFactory();
        using var client = CreateWriterClient(factory);

        await CreateOriginalEntry(client);

        using var response = await client.PutAsJsonAsync(
            Url,
            new
            {
                value = "invalid-update",
                ttlSeconds
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        await AssertOriginalValueUnchanged(client);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Put_WithInvalidMetadataKey_ShouldReturn400(
        string metadataKey)
    {
        using var factory = new TestApiFactory();
        using var client = CreateWriterClient(factory);

        await CreateOriginalEntry(client);

        using var response = await client.PutAsJsonAsync(
            Url,
            new
            {
                value = "invalid-update",
                metadata = new Dictionary<string, string>
                {
                    [metadataKey] = "value"
                }
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        await AssertOriginalValueUnchanged(client);
    }

    [Fact]
    public async Task Put_WithNullMetadataValue_ShouldReturn400()
    {
        using var factory = new TestApiFactory();
        using var client = CreateWriterClient(factory);

        await CreateOriginalEntry(client);

        using var response = await client.PutAsJsonAsync(
            Url,
            new
            {
                value = "invalid-update",
                metadata = new Dictionary<string, string?>
                {
                    ["source"] = null
                }
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        await AssertOriginalValueUnchanged(client);
    }

    [Fact]
    public async Task Put_WithValidTtlAndMetadata_ShouldSucceed()
    {
        using var factory = new TestApiFactory();
        using var client = CreateWriterClient(factory);

        using var response = await client.PutAsJsonAsync(
            Url,
            new
            {
                value = "valid-value",
                ttlSeconds = 3600,
                metadata = new Dictionary<string, string>
                {
                    ["source"] = "integration-test"
                }
            });

        Assert.Equal(
            HttpStatusCode.NoContent,
            response.StatusCode);

        using var get = await client.GetAsync(Url);

        Assert.Equal(
            HttpStatusCode.OK,
            get.StatusCode);

        JsonElement body =
            await get.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "valid-value",
            body.GetProperty("value").GetString());

        Assert.Equal(
            "integration-test",
            body.GetProperty("metadata")
                .GetProperty("source")
                .GetString());
    }

    private static async Task CreateOriginalEntry(
        HttpClient client)
    {
        using var response = await client.PutAsJsonAsync(
            Url,
            new { value = "original-value" });

        Assert.Equal(
            HttpStatusCode.NoContent,
            response.StatusCode);
    }

    private static async Task AssertOriginalValueUnchanged(
        HttpClient client)
    {
        using var response = await client.GetAsync(Url);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        JsonElement body =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "original-value",
            body.GetProperty("value").GetString());
    }

    private static HttpClient CreateWriterClient(
        TestApiFactory factory)
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
            "validation-space",
            UserRole.Writer));

        var (credential, plaintextKey) = tokens.Issue(
            userId,
            DateTimeOffset.UtcNow);

        credentials.Save(credential);

        var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-API-Key",
            plaintextKey);

        return client;
    }
}