using System.Text.Json;
using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Security;

namespace KeyValueStore.Infrastructure.Security;

public sealed class JsonApiKeyCredentialRepository
    : IApiKeyCredentialRepository
{
    private readonly object _sync = new();
    private readonly string _filePath;

    private Dictionary<Guid, ApiKeyCredential> _credentials;

    private sealed record StoredCredential(
        Guid Id,
        Guid UserId,
        string SecretHash,
        DateTimeOffset CreatedAt,
        DateTimeOffset? RevokedAt);

    public JsonApiKeyCredentialRepository(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "Credential file path cannot be empty.",
                nameof(filePath));
        }

        _filePath = Path.GetFullPath(filePath);
        _credentials = LoadFromFile();
    }

    public ApiKeyCredential? GetById(Guid keyId)
    {
        lock (_sync)
        {
            return _credentials.TryGetValue(
                keyId,
                out var credential)
                ? Clone(credential)
                : null;
        }
    }

    public IReadOnlyList<ApiKeyCredential> GetByUserId(
        Guid userId)
    {
        lock (_sync)
        {
            return _credentials.Values
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.CreatedAt)
                .Select(Clone)
                .ToList();
        }
    }

    public void Save(ApiKeyCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);

        lock (_sync)
        {
            var copy = Clone(credential);

            if (_credentials.TryGetValue(
                copy.Id,
                out var existing))
            {
                ValidateUpdate(existing, copy);
            }

            var updated =
                new Dictionary<Guid, ApiKeyCredential>(
                    _credentials)
                {
                    [copy.Id] = copy
                };

            WriteToFile(updated);

            _credentials = updated;
        }
    }

    public void Rotate(
        Guid oldKeyId,
        ApiKeyCredential replacement,
        DateTimeOffset now)
    {
        if (oldKeyId == Guid.Empty)
        {
            throw new ArgumentException(
                "Old API key ID cannot be empty.",
                nameof(oldKeyId));
        }

        ArgumentNullException.ThrowIfNull(replacement);

        lock (_sync)
        {
            if (!_credentials.TryGetValue(
                oldKeyId,
                out var oldCredential))
            {
                throw new InvalidOperationException(
                    "Original API key does not exist.");
            }

            if (oldCredential.IsRevoked)
            {
                throw new InvalidOperationException(
                    "Original API key is already revoked.");
            }

            if (replacement.Id == oldKeyId ||
                _credentials.ContainsKey(replacement.Id))
            {
                throw new InvalidOperationException(
                    "Replacement API key ID already exists.");
            }

            if (replacement.UserId != oldCredential.UserId)
            {
                throw new InvalidOperationException(
                    "Replacement must belong to the same user.");
            }

            if (replacement.IsRevoked)
            {
                throw new InvalidOperationException(
                    "Replacement API key must be active.");
            }

            if (now < oldCredential.CreatedAt ||
                replacement.CreatedAt != now)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(now),
                    "Invalid rotation timestamp.");
            }

            var revokedOld = Clone(oldCredential);
            revokedOld.Revoke(now);

            var newCredential = Clone(replacement);

            var updated =
                new Dictionary<Guid, ApiKeyCredential>(
                    _credentials)
                {
                    [oldKeyId] = revokedOld,
                    [newCredential.Id] = newCredential
                };

            WriteToFile(updated);

            _credentials = updated;
        }
    }

    private static void ValidateUpdate(
        ApiKeyCredential existing,
        ApiKeyCredential updated)
    {
        if (existing.UserId != updated.UserId ||
            existing.SecretHash != updated.SecretHash ||
            existing.CreatedAt != updated.CreatedAt)
        {
            throw new InvalidOperationException(
                "Credential identity cannot be changed.");
        }

        if (existing.IsRevoked &&
            existing.RevokedAt != updated.RevokedAt)
        {
            throw new InvalidOperationException(
                "A revoked credential cannot be reactivated or modified.");
        }
    }

    private Dictionary<Guid, ApiKeyCredential> LoadFromFile()
    {
        var result =
            new Dictionary<Guid, ApiKeyCredential>();

        if (!File.Exists(_filePath))
            return result;

        List<StoredCredential> stored;

        try
        {
            stored = JsonSerializer.Deserialize<
                List<StoredCredential>>(
                    File.ReadAllBytes(_filePath))
                ?? throw new InvalidDataException(
                    "Credential file cannot be null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "Credential file contains invalid JSON.",
                ex);
        }

        foreach (var item in stored)
        {
            if (item is null)
            {
                throw new InvalidDataException(
                    "Credential file contains a null entry.");
            }

            ApiKeyCredential credential;

            try
            {
                credential = new ApiKeyCredential(
                    item.Id,
                    item.UserId,
                    item.SecretHash,
                    item.CreatedAt);

                if (item.RevokedAt is not null)
                {
                    credential.Revoke(item.RevokedAt.Value);
                }
            }
            catch (ArgumentException ex)
            {
                throw new InvalidDataException(
                    "Credential file contains invalid data.",
                    ex);
            }

            if (!result.TryAdd(credential.Id, credential))
            {
                throw new InvalidDataException(
                    "Credential file contains duplicate IDs.");
            }
        }

        return result;
    }

    private void WriteToFile(
        Dictionary<Guid, ApiKeyCredential> credentials)
    {
        string directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException(
                "Invalid credential file path.");

        Directory.CreateDirectory(directory);

        var stored = credentials.Values
            .OrderBy(c => c.Id)
            .Select(c => new StoredCredential(
                c.Id,
                c.UserId,
                c.SecretHash,
                c.CreatedAt,
                c.RevokedAt))
            .ToArray();

        string temporaryPath =
            $"{_filePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                JsonSerializer.Serialize(
                    stream,
                    stored,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                stream.Flush(flushToDisk: true);
            }

            File.Move(
                temporaryPath,
                _filePath,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static ApiKeyCredential Clone(
        ApiKeyCredential original)
    {
        var copy = new ApiKeyCredential(
            original.Id,
            original.UserId,
            original.SecretHash,
            original.CreatedAt);

        if (original.RevokedAt is not null)
        {
            copy.Revoke(original.RevokedAt.Value);
        }

        return copy;
    }
}