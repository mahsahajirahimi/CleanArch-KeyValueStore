using KeyValueStore.Domain.Security;

namespace KeyValueStore.Application.Abstractions;

public interface IApiKeyCredentialRepository
{
    ApiKeyCredential? GetById(Guid keyId);

    IReadOnlyList<ApiKeyCredential> GetByUserId(Guid userId);

    void Save(ApiKeyCredential credential);
}