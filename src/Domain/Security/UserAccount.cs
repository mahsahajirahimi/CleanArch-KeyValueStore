namespace KeyValueStore.Domain.Security;

public sealed class UserAccount
{
    public Guid Id { get; }

    public string Keyspace { get; }

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public UserAccount(
        Guid id,
        string keyspace,
        UserRole role)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "User ID cannot be empty.",
                nameof(id));
        }

        if (string.IsNullOrWhiteSpace(keyspace))
        {
            throw new ArgumentException(
                "Keyspace cannot be empty.",
                nameof(keyspace));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(
                nameof(role));
        }

        Id = id;
        Keyspace = keyspace;
        Role = role;
        IsActive = true;
    }

    public void ChangeRole(UserRole newRole)
    {
        if (!Enum.IsDefined(newRole))
        {
            throw new ArgumentOutOfRangeException(
                nameof(newRole));
        }

        Role = newRole;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}