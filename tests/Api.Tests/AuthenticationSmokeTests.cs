using System.Net;
using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Security;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KeyValueStore.Api.Tests;

public sealed class AuthenticationSmokeTests
{
    [Fact]
    public async Task Get_WithoutApiKey_ShouldReturn401()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/keyspaces/audit-space/entries/missing");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Get_WithValidApiKey_ShouldReachController()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        Guid userId = Guid.NewGuid();

        var users = factory.Services
            .GetRequiredService<IUserAccountRepository>();

        users.Save(new UserAccount(
            userId,
            "audit-space",
            UserRole.Reader));

        var tokens = factory.Services
            .GetRequiredService<IApiKeyTokenService>();

        var credentials = factory.Services
            .GetRequiredService<IApiKeyCredentialRepository>();

        var (credential, plaintextKey) = tokens.Issue(
            userId,
            DateTimeOffset.UtcNow);

        credentials.Save(credential);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/keyspaces/audit-space/entries/missing");

        request.Headers.Add(
            "X-API-Key",
            plaintextKey);

        using var response =
            await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        Assert.True(File.Exists(
            Path.Combine(
                factory.DataDirectory,
                "users.json")));

        Assert.True(File.Exists(
            Path.Combine(
                factory.DataDirectory,
                "credentials.json")));
    }
}
