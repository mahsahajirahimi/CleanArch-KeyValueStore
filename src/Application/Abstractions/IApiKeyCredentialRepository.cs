using KeyValueStore.Domain.Security;

namespace KeyValueStore.Application.Abstractions;

public interface IApiKeyCredentialRepository
{
    ApiKeyCredential? GetById(Guid keyId);

    IReadOnlyList<ApiKeyCredential> GetByUserId(Guid userId);

    void Save(ApiKeyCredential credential);

    bool Revoke(Guid keyId, DateTimeOffset now);

    void Rotate(
        Guid oldKeyId,
        ApiKeyCredential replacement,
        DateTimeOffset now);
}