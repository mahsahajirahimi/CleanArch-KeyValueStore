using System.Text.Json;
using System.Text.Json.Serialization;
using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Security;

namespace KeyValueStore.Infrastructure.Security;

public sealed class JsonUserAccountRepository
    : IUserAccountRepository
{
    private readonly object _sync = new();
    private readonly string _filePath;

    private Dictionary<Guid, UserAccount> _users;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter<UserRole>() }
    };

    private sealed record StoredUser(
        Guid Id,
        string Keyspace,
        UserRole Role,
        bool IsActive);

    public JsonUserAccountRepository(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "User file path cannot be empty.",
                nameof(filePath));
        }

        _filePath = Path.GetFullPath(filePath);
        _users = LoadFromFile();
    }

    public UserAccount? GetById(Guid userId)
    {
        lock (_sync)
        {
            return _users.TryGetValue(userId, out var user)
                ? Clone(user)
                : null;
        }
    }

    public void Save(UserAccount user)
    {
        ArgumentNullException.ThrowIfNull(user);

        lock (_sync)
        {
            var copy = Clone(user);

            if (_users.TryGetValue(
                copy.Id,
                out var existing))
            {
                ValidateUpdate(existing, copy);
            }

            var updated = new Dictionary<Guid, UserAccount>(
                _users)
            {
                [copy.Id] = copy
            };

            WriteToFile(updated);

            _users = updated;
        }
    }

    private static void ValidateUpdate(
        UserAccount existing,
        UserAccount updated)
    {
        if (!string.Equals(
                existing.Keyspace,
                updated.Keyspace,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "User keyspace cannot be changed.");
        }

        if (!existing.IsActive && updated.IsActive)
        {
            throw new InvalidOperationException(
                "A deactivated user cannot be reactivated.");
        }
    }

    private Dictionary<Guid, UserAccount> LoadFromFile()
    {
        var result = new Dictionary<Guid, UserAccount>();

        if (!File.Exists(_filePath))
            return result;

        List<StoredUser> stored;

        try
        {
            stored = JsonSerializer.Deserialize<List<StoredUser>>(
                File.ReadAllBytes(_filePath),
                JsonOptions)
                ?? throw new InvalidDataException(
                    "User file cannot be null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "User file contains invalid JSON.",
                ex);
        }

        foreach (var item in stored)
        {
            if (item is null)
            {
                throw new InvalidDataException(
                    "User file contains a null entry.");
            }

            UserAccount user;

            try
            {
                user = new UserAccount(
                    item.Id,
                    item.Keyspace,
                    item.Role);

                if (!item.IsActive)
                    user.Deactivate();
            }
            catch (ArgumentException ex)
            {
                throw new InvalidDataException(
                    "User file contains invalid data.",
                    ex);
            }

            if (!result.TryAdd(user.Id, user))
            {
                throw new InvalidDataException(
                    "User file contains duplicate IDs.");
            }
        }

        return result;
    }

    private void WriteToFile(
        Dictionary<Guid, UserAccount> users)
    {
        string directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException(
                "Invalid user file path.");

        Directory.CreateDirectory(directory);

        var stored = users.Values
            .OrderBy(user => user.Id)
            .Select(user => new StoredUser(
                user.Id,
                user.Keyspace,
                user.Role,
                user.IsActive))
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
                    JsonOptions);

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

    private static UserAccount Clone(UserAccount original)
    {
        var copy = new UserAccount(
            original.Id,
            original.Keyspace,
            original.Role);

        if (!original.IsActive)
            copy.Deactivate();

        return copy;
    }

    public bool HasAny()
    {
        lock (_sync)
        {
            return _users.Count > 0;
        }
    }

    public bool HasKeyspace(string keyspace)
    {
        if (string.IsNullOrWhiteSpace(keyspace))
        {
            throw new ArgumentException(
                "Keyspace cannot be empty.",
                nameof(keyspace));
        }

        lock (_sync)
        {
            return _users.Values.Any(user =>
                string.Equals(
                    user.Keyspace,
                    keyspace,
                    StringComparison.Ordinal));
        }
    }
}