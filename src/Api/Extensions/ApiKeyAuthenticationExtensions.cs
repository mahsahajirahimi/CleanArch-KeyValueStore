using KeyValueStore.Api.Authentication;
using KeyValueStore.Application.Abstractions;
using KeyValueStore.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;

namespace KeyValueStore.Api.Extensions;

public static class ApiKeyAuthenticationExtensions
{
    public static IServiceCollection AddApiKeyAuthentication(
        this IServiceCollection services,
        string dataDirectory)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            throw new ArgumentException(
                "Data directory cannot be empty.",
                nameof(dataDirectory));
        }

        string directory = Path.GetFullPath(dataDirectory);

        services.AddSingleton<IUserAccountRepository>(
            _ => new JsonUserAccountRepository(
                Path.Combine(directory, "users.json")));

        services.AddSingleton<IApiKeyCredentialRepository>(
            _ => new JsonApiKeyCredentialRepository(
                Path.Combine(directory, "credentials.json")));

        services.AddSingleton<ApiKeyTokenService>();
        services.AddSingleton<ApiKeyAuthenticationService>();

        services.AddAuthentication(
            ApiKeyAuthenticationHandler.SchemeName)
            .AddScheme<
                AuthenticationSchemeOptions,
                ApiKeyAuthenticationHandler>(
                    ApiKeyAuthenticationHandler.SchemeName,
                    _ => { });

        services.AddAuthorization();

        return services;
    }
}