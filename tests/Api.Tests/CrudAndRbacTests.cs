using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Security;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KeyValueStore.Api.Tests;

public sealed class CrudAndRbacTests
{
    private const string Keyspace = "test-space";

    [Fact]
    public async Task Writer_CanPerformCrud()
    {
        using var factory = new TestApiFactory();

        string writerKey = CreateApiKey(
            factory, UserRole.Writer, Keyspace);

        using var client = CreateClient(factory, writerKey);

        string url =
            $"/api/keyspaces/{Keyspace}/entries/crud-test";

        using var put = await client.PutAsJsonAsync(
            url, new { value = "hello-writer" });

        Assert.Equal(
            HttpStatusCode.NoContent,
            put.StatusCode);

        using var get = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        JsonElement body =
            await get.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "hello-writer",
            body.GetProperty("value").GetString());

        using var delete = await client.DeleteAsync(url);

        Assert.Equal(
            HttpStatusCode.NoContent,
            delete.StatusCode);

        using var missing = await client.GetAsync(url);

        Assert.Equal(
            HttpStatusCode.NotFound,
            missing.StatusCode);
    }

    [Fact]
    public async Task Reader_CanReadButCannotModify()
    {
        using var factory = new TestApiFactory();

        string writerKey = CreateApiKey(
            factory, UserRole.Writer, Keyspace);

        string readerKey = CreateApiKey(
            factory, UserRole.Reader, Keyspace);

        using var writer = CreateClient(factory, writerKey);
        using var reader = CreateClient(factory, readerKey);

        string url =
            $"/api/keyspaces/{Keyspace}/entries/rbac-test";

        using var setup = await writer.PutAsJsonAsync(
            url, new { value = "original-value" });

        Assert.Equal(
            HttpStatusCode.NoContent,
            setup.StatusCode);

        using var read = await reader.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        using var forbiddenPut = await reader.PutAsJsonAsync(
            url, new { value = "reader-overwrite" });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            forbiddenPut.StatusCode);

        using var forbiddenDelete =
            await reader.DeleteAsync(url);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            forbiddenDelete.StatusCode);

        using var verify = await writer.GetAsync(url);

        Assert.Equal(
            HttpStatusCode.OK,
            verify.StatusCode);

        JsonElement body =
            await verify.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "original-value",
            body.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Users_CannotAccessOtherKeyspaces()
    {
        using var factory = new TestApiFactory();

        string readerKey = CreateApiKey(
            factory, UserRole.Reader, Keyspace);

        string writerKey = CreateApiKey(
            factory, UserRole.Writer, Keyspace);

        using var reader = CreateClient(factory, readerKey);
        using var writer = CreateClient(factory, writerKey);

        const string foreignUrl =
            "/api/keyspaces/other-space/entries/secret";

        using var read = await reader.GetAsync(foreignUrl);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            read.StatusCode);

        using var write = await writer.PutAsJsonAsync(
            foreignUrl, new { value = "not-allowed" });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            write.StatusCode);
    }

    [Fact]
    public async Task ReaderAndWriter_CannotAccessAdminApi()
    {
        using var factory = new TestApiFactory();

        string readerKey = CreateApiKey(
            factory, UserRole.Reader, Keyspace);

        string writerKey = CreateApiKey(
            factory, UserRole.Writer, Keyspace);

        using var reader = CreateClient(factory, readerKey);
        using var writer = CreateClient(factory, writerKey);

        const string url = "/api/admin/users";

        using var readerResponse =
            await reader.PostAsJsonAsync(
                url, new { role = "Reader" });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            readerResponse.StatusCode);

        using var writerResponse =
            await writer.PostAsJsonAsync(
                url, new { role = "Reader" });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            writerResponse.StatusCode);
    }

    private static string CreateApiKey(
        TestApiFactory factory,
        UserRole role,
        string keyspace)
    {
        using var scope = factory.Services.CreateScope();

        var services = scope.ServiceProvider;

        var users = services
            .GetRequiredService<IUserAccountRepository>();

        var credentials = services
            .GetRequiredService<IApiKeyCredentialRepository>();

        var tokens = services
            .GetRequiredService<IApiKeyTokenService>();

        Guid userId = Guid.NewGuid();

        users.Save(new UserAccount(
            userId, keyspace, role));

        var (credential, plaintextKey) = tokens.Issue(
            userId, DateTimeOffset.UtcNow);

        credentials.Save(credential);

        return plaintextKey;
    }

    private static HttpClient CreateClient(
        TestApiFactory factory,
        string apiKey)
    {
        var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-API-Key", apiKey);

        return client;
    }
}