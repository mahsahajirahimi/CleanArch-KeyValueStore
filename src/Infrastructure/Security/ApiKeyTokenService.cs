using System.Security.Cryptography;
using System.Text;
using KeyValueStore.Domain.Security;
using KeyValueStore.Application.Abstractions;

namespace KeyValueStore.Infrastructure.Security;

public sealed class ApiKeyTokenService : IApiKeyTokenService
{
    private const string Prefix = "kvs_";
    private const int SecretBytes = 32;
    private const int KeyIdLength = 32;
    private const int EncodedSecretLength = 43;

    public (
        ApiKeyCredential Credential,
        string PlaintextKey
    ) Issue(Guid userId, DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException(
                "User ID cannot be empty.",
                nameof(userId));

        Guid keyId = Guid.NewGuid();

        byte[] randomBytes =
            RandomNumberGenerator.GetBytes(SecretBytes);

        string secret = Convert.ToBase64String(randomBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        string secretHash = ComputeHash(secret);

        var credential = new ApiKeyCredential(
            keyId,
            userId,
            secretHash,
            createdAt);

        string plaintextKey =
            $"{Prefix}{keyId:N}.{secret}";

        return (credential, plaintextKey);
    }

    public bool Verify(
        ApiKeyCredential credential,
        string? presentedKey)
    {
        ArgumentNullException.ThrowIfNull(credential);

        if (credential.IsRevoked)
            return false;

        if (!TryParse(
                presentedKey,
                out Guid keyId,
                out string secret))
        {
            return false;
        }

        if (keyId != credential.Id)
            return false;

        byte[] actualHash = SHA256.HashData(
            Encoding.UTF8.GetBytes(secret));

        byte[] expectedHash =
            Convert.FromHexString(credential.SecretHash);

        return CryptographicOperations.FixedTimeEquals(
            actualHash,
            expectedHash);
    }

    private static string ComputeHash(string secret)
    {
        byte[] hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(secret));

        return Convert.ToHexString(hash);
    }

    private static bool TryParse(
        string? apiKey,
        out Guid keyId,
        out string secret)
    {
        keyId = Guid.Empty;
        secret = string.Empty;

        if (apiKey is null ||
            !apiKey.StartsWith(
                Prefix,
                StringComparison.Ordinal))
        {
            return false;
        }

        int dotIndex = Prefix.Length + KeyIdLength;

        if (apiKey.Length !=
                dotIndex + 1 + EncodedSecretLength ||
            apiKey[dotIndex] != '.')
        {
            return false;
        }

        if (!Guid.TryParseExact(
                apiKey.AsSpan(Prefix.Length, KeyIdLength),
                "N",
                out keyId))
        {
            return false;
        }

        secret = apiKey[(dotIndex + 1)..];

        foreach (char character in secret)
        {
            if (!char.IsAsciiLetterOrDigit(character) &&
                character != '-' &&
                character != '_')
            {
                return false;
            }
        }

        return true;
    }

    public bool TryGetKeyId(
        string? presentedKey,
        out Guid keyId)
    {
        return TryParse(
            presentedKey,
            out keyId,
            out _);
    }

}