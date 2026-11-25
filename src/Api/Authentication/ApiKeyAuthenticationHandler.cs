using System.Security.Claims;
using System.Text.Encodings.Web;
using KeyValueStore.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace KeyValueStore.Api.Authentication;

public sealed class ApiKeyAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";

    private const string HeaderName = "X-API-Key";

    private readonly ApiKeyAuthenticationService _authentication;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ApiKeyAuthenticationService authentication)
        : base(options, logger, encoder)
    {
        _authentication = authentication;
    }

    protected override Task<AuthenticateResult>
        HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(
            HeaderName,
            out var headers))
        {
            return Task.FromResult(
                AuthenticateResult.NoResult());
        }

        if (headers.Count != 1 ||
            string.IsNullOrWhiteSpace(headers[0]) ||
            headers[0]!.Length > 256)
        {
            return Task.FromResult(
                AuthenticateResult.Fail("Invalid API key."));
        }

        AuthenticatedApiKeyUser? user =
            _authentication.Authenticate(headers[0]);

        if (user is null)
        {
            return Task.FromResult(
                AuthenticateResult.Fail("Invalid API key."));
        }

        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                user.UserId.ToString()),

            new Claim(
                ClaimTypes.Role,
                user.Role.ToString()),

            new Claim("keyspace", user.Keyspace),

            new Claim(
                "api_key_id",
                user.KeyId.ToString())
        };

        var identity = new ClaimsIdentity(
            claims,
            SchemeName);

        var principal = new ClaimsPrincipal(identity);

        var ticket = new AuthenticationTicket(
            principal,
            SchemeName);

        return Task.FromResult(
            AuthenticateResult.Success(ticket));
    }
}