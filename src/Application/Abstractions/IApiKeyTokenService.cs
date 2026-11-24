using KeyValueStore.Domain.Security;

namespace KeyValueStore.Application.Abstractions;

public interface IApiKeyTokenService
{
    (ApiKeyCredential Credential, string PlaintextKey)
        Issue(Guid userId, DateTimeOffset createdAt);
}