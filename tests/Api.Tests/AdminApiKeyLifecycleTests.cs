using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Security;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KeyValueStore.Api.Tests;

public sealed class AdminApiKeyLifecycleTests
{
    [Fact]
    public async Task Admin_CanManageApiKeyLifecycle()
    {
        using var factory = new TestApiFactory();

        var (_, adminKey) = SeedUser(
            factory, "team-a", UserRole.Admin);

        using var admin = CreateClient(factory, adminKey);

        using var create = await admin.PostAsJsonAsync(
            "/api/admin/users",
            new { role = "Reader" });

        Assert.Equal(
            HttpStatusCode.Created,
            create.StatusCode);

        JsonElement created =
            await create.Content.ReadFromJsonAsync<JsonElement>();

        Guid userId = created.GetProperty("userId").GetGuid();

        Assert.Equal(
            "team-a",
            created.GetProperty("keyspace").GetString());

        Assert.Equal(
            "Reader",
            created.GetProperty("role").GetString());

        using var issue = await admin.PostAsync(
            $"/api/admin/users/{userId}/api-keys",
            null);

        Assert.Equal(
            HttpStatusCode.Created,
            issue.StatusCode);

        JsonElement issued =
            await issue.Content.ReadFromJsonAsync<JsonElement>();

        Guid oldKeyId =
            issued.GetProperty("keyId").GetGuid();

        string oldKey =
            issued.GetProperty("apiKey").GetString()!;

        Assert.Equal(
            userId,
            issued.GetProperty("userId").GetGuid());

        Assert.StartsWith("kvs_", oldKey);

        using var oldClient = CreateClient(factory, oldKey);

        const string entryUrl =
            "/api/keyspaces/team-a/entries/missing";

        using var beforeRotate =
            await oldClient.GetAsync(entryUrl);

        Assert.Equal(
            HttpStatusCode.NotFound,
            beforeRotate.StatusCode);

        using var rotate = await admin.PostAsync(
            $"/api/admin/api-keys/{oldKeyId}/rotate",
            null);

        Assert.Equal(
            HttpStatusCode.Created,
            rotate.StatusCode);

        JsonElement rotated =
            await rotate.Content.ReadFromJsonAsync<JsonElement>();

        Guid newKeyId =
            rotated.GetProperty("keyId").GetGuid();

        string newKey =
            rotated.GetProperty("apiKey").GetString()!;

        Assert.NotEqual(oldKeyId, newKeyId);
        Assert.NotEqual(oldKey, newKey);

        using var oldAfterRotate =
            await oldClient.GetAsync(entryUrl);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            oldAfterRotate.StatusCode);

        using var newClient = CreateClient(factory, newKey);

        using var newAfterRotate =
            await newClient.GetAsync(entryUrl);

        Assert.Equal(
            HttpStatusCode.NotFound,
            newAfterRotate.StatusCode);

        using var repeatedRotate = await admin.PostAsync(
            $"/api/admin/api-keys/{oldKeyId}/rotate",
            null);

        Assert.Equal(
            HttpStatusCode.Conflict,
            repeatedRotate.StatusCode);

        using var revoke = await admin.DeleteAsync(
            $"/api/admin/api-keys/{newKeyId}");

        Assert.Equal(
            HttpStatusCode.NoContent,
            revoke.StatusCode);

        using var afterRevoke =
            await newClient.GetAsync(entryUrl);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            afterRevoke.StatusCode);

        using var repeatedRevoke = await admin.DeleteAsync(
            $"/api/admin/api-keys/{newKeyId}");

        Assert.Equal(
            HttpStatusCode.NoContent,
            repeatedRevoke.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotManageAnotherKeyspace()
    {
        using var factory = new TestApiFactory();

        var (_, adminAKey) = SeedUser(
            factory, "team-a", UserRole.Admin);

        var (_, adminBKey) = SeedUser(
            factory, "team-b", UserRole.Admin);

        using var adminA = CreateClient(factory, adminAKey);
        using var adminB = CreateClient(factory, adminBKey);

        using var create = await adminB.PostAsJsonAsync(
            "/api/admin/users",
            new { role = "Reader" });

        Assert.Equal(
            HttpStatusCode.Created,
            create.StatusCode);

        JsonElement created =
            await create.Content.ReadFromJsonAsync<JsonElement>();

        Guid userBId =
            created.GetProperty("userId").GetGuid();

        using var issue = await adminB.PostAsync(
            $"/api/admin/users/{userBId}/api-keys",
            null);

        Assert.Equal(
            HttpStatusCode.Created,
            issue.StatusCode);

        JsonElement issued =
            await issue.Content.ReadFromJsonAsync<JsonElement>();

        Guid keyBId =
            issued.GetProperty("keyId").GetGuid();

        string keyB =
            issued.GetProperty("apiKey").GetString()!;

        using var foreignIssue = await adminA.PostAsync(
            $"/api/admin/users/{userBId}/api-keys",
            null);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            foreignIssue.StatusCode);

        using var foreignRotate = await adminA.PostAsync(
            $"/api/admin/api-keys/{keyBId}/rotate",
            null);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            foreignRotate.StatusCode);

        using var foreignRevoke = await adminA.DeleteAsync(
            $"/api/admin/api-keys/{keyBId}");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            foreignRevoke.StatusCode);

        using var userB = CreateClient(factory, keyB);

        using var verify = await userB.GetAsync(
            "/api/keyspaces/team-b/entries/missing");

        Assert.Equal(
            HttpStatusCode.NotFound,
            verify.StatusCode);
    }

    [Fact]
    public async Task NonAdmin_CannotManageApiKeys()
    {
        using var factory = new TestApiFactory();

        var (_, adminKey) = SeedUser(
            factory, "team-a", UserRole.Admin);

        var (readerId, readerKey) = SeedUser(
            factory, "team-a", UserRole.Reader);

        using var admin = CreateClient(factory, adminKey);
        using var reader = CreateClient(factory, readerKey);
        using var anonymous = factory.CreateClient();

        using var issue = await admin.PostAsync(
            $"/api/admin/users/{readerId}/api-keys",
            null);

        Assert.Equal(
            HttpStatusCode.Created,
            issue.StatusCode);

        JsonElement issued =
            await issue.Content.ReadFromJsonAsync<JsonElement>();

        Guid keyId =
            issued.GetProperty("keyId").GetGuid();

        using var forbiddenIssue = await reader.PostAsync(
            $"/api/admin/users/{readerId}/api-keys",
            null);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            forbiddenIssue.StatusCode);

        using var forbiddenRotate = await reader.PostAsync(
            $"/api/admin/api-keys/{keyId}/rotate",
            null);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            forbiddenRotate.StatusCode);

        using var forbiddenRevoke = await reader.DeleteAsync(
            $"/api/admin/api-keys/{keyId}");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            forbiddenRevoke.StatusCode);

        using var unauthenticated = await anonymous.PostAsJsonAsync(
            "/api/admin/users",
            new { role = "Reader" });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            unauthenticated.StatusCode);

        using var invalidRole = await admin.PostAsJsonAsync(
            "/api/admin/users",
            new { role = "SuperAdmin" });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            invalidRole.StatusCode);
    }

    [Fact]
    public async Task ConcurrentRevokeRequests_ShouldBothReturn204()
    {
        using var factory = new TestApiFactory();

        var (_, adminKey) = SeedUser(
            factory, "team-a", UserRole.Admin);

        var (readerId, readerKey) = SeedUser(
            factory, "team-a", UserRole.Reader);

        Guid keyId;
        using (var scope = factory.Services.CreateScope())
        {
            var credentials = scope.ServiceProvider
                .GetRequiredService<IApiKeyCredentialRepository>();

            keyId = Assert.Single(
                credentials.GetByUserId(readerId)).Id;
        }

        using var admin = CreateClient(factory, adminKey);

        string url = $"/api/admin/api-keys/{keyId}";

        var requests = new[]
        {
            admin.DeleteAsync(url),
            admin.DeleteAsync(url)
        };

        HttpResponseMessage[] responses = await Task.WhenAll(requests);

        try
        {
            Assert.All(responses, response =>
                Assert.Equal(
                    HttpStatusCode.NoContent,
                    response.StatusCode));
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }

        using var reader = CreateClient(factory, readerKey);

        using var check = await reader.GetAsync(
            "/api/keyspaces/team-a/entries/missing");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            check.StatusCode);
    }

    private static (Guid UserId, string ApiKey) SeedUser(
        TestApiFactory factory,
        string keyspace,
        UserRole role)
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

        return (userId, plaintextKey);
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
}