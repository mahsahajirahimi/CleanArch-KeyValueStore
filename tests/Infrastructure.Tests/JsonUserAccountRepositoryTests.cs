using System.Text.Json;
using KeyValueStore.Domain.Security;
using KeyValueStore.Infrastructure.Security;
using Xunit;

namespace KeyValueStore.Infrastructure.Tests;

public sealed class JsonUserAccountRepositoryTests
    : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"KeyValueStoreUserTests-{Guid.NewGuid():N}");

    private string StorePath =>
        Path.Combine(_directory, "users.json");

    [Fact]
    public void GetById_ShouldReturnNullForUnknownUser()
    {
        var repository =
            new JsonUserAccountRepository(StorePath);

        Assert.Null(repository.GetById(Guid.NewGuid()));
    }

    [Fact]
    public void Save_ShouldRestoreUsersAfterRestart()
    {
        var repository =
            new JsonUserAccountRepository(StorePath);

        var admin = CreateUser("userA", UserRole.Admin);
        var reader = CreateUser("userB", UserRole.Reader);

        repository.Save(admin);
        repository.Save(reader);

        var restarted =
            new JsonUserAccountRepository(StorePath);

        var restoredAdmin = restarted.GetById(admin.Id);
        var restoredReader = restarted.GetById(reader.Id);

        Assert.NotNull(restoredAdmin);
        Assert.Equal(UserRole.Admin, restoredAdmin.Role);
        Assert.Equal("userA", restoredAdmin.Keyspace);

        Assert.NotNull(restoredReader);
        Assert.Equal(UserRole.Reader, restoredReader.Role);
        Assert.Equal("userB", restoredReader.Keyspace);
    }

    [Fact]
    public void Save_ShouldPersistRoleChanges()
    {
        var repository =
            new JsonUserAccountRepository(StorePath);

        var user = CreateUser("userA", UserRole.Reader);

        repository.Save(user);

        user.ChangeRole(UserRole.Writer);
        repository.Save(user);

        var restarted =
            new JsonUserAccountRepository(StorePath);

        Assert.Equal(
            UserRole.Writer,
            restarted.GetById(user.Id)?.Role);
    }

    [Fact]
    public void Save_ShouldPersistDeactivation()
    {
        var repository =
            new JsonUserAccountRepository(StorePath);

        var user = CreateUser("userA", UserRole.Admin);

        repository.Save(user);

        user.Deactivate();
        repository.Save(user);

        var restarted =
            new JsonUserAccountRepository(StorePath);

        Assert.False(restarted.GetById(user.Id)?.IsActive);
    }

    [Fact]
    public void GetById_ShouldReturnDefensiveCopy()
    {
        var repository =
            new JsonUserAccountRepository(StorePath);

        var user = CreateUser("userA", UserRole.Reader);

        repository.Save(user);

        var fetched = repository.GetById(user.Id);

        Assert.NotNull(fetched);

        fetched.ChangeRole(UserRole.Admin);

        var stored = repository.GetById(user.Id);

        Assert.NotNull(stored);
        Assert.Equal(UserRole.Reader, stored.Role);
    }

    [Fact]
    public void Save_ShouldRejectKeyspaceChange()
    {
        var repository =
            new JsonUserAccountRepository(StorePath);

        var user = CreateUser("userA", UserRole.Reader);
        repository.Save(user);

        var modified = new UserAccount(
            user.Id,
            "userB",
            UserRole.Reader);

        Assert.Throws<InvalidOperationException>(() =>
            repository.Save(modified));

        Assert.Equal(
            "userA",
            repository.GetById(user.Id)?.Keyspace);
    }

    [Fact]
    public void Save_ShouldRejectReactivation()
    {
        var repository =
            new JsonUserAccountRepository(StorePath);

        var user = CreateUser("userA", UserRole.Writer);

        user.Deactivate();
        repository.Save(user);

        var activeCopy = new UserAccount(
            user.Id,
            user.Keyspace,
            user.Role);

        Assert.Throws<InvalidOperationException>(() =>
            repository.Save(activeCopy));

        Assert.False(repository.GetById(user.Id)?.IsActive);
    }

    [Fact]
    public void Startup_ShouldRejectCorruptedUserFile()
    {
        Directory.CreateDirectory(_directory);

        File.WriteAllText(StorePath, "{invalid json");

        string originalContent =
            File.ReadAllText(StorePath);

        Assert.Throws<InvalidDataException>(() =>
            new JsonUserAccountRepository(StorePath));

        Assert.Equal(
            originalContent,
            File.ReadAllText(StorePath));
    }

    [Fact]
    public void Startup_ShouldRejectDuplicateUserIds()
    {
        Directory.CreateDirectory(_directory);

        Guid id = Guid.NewGuid();

        var duplicates = new[]
        {
            new
            {
                Id = id,
                Keyspace = "userA",
                Role = 0,
                IsActive = true
            },
            new
            {
                Id = id,
                Keyspace = "userA",
                Role = 1,
                IsActive = true
            }
        };

        File.WriteAllText(
            StorePath,
            JsonSerializer.Serialize(duplicates));

        Assert.Throws<InvalidDataException>(() =>
            new JsonUserAccountRepository(StorePath));
    }

    private static UserAccount CreateUser(
        string keyspace,
        UserRole role)
    {
        return new UserAccount(
            Guid.NewGuid(),
            keyspace,
            role);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(
                _directory,
                recursive: true);
        }
    }
}