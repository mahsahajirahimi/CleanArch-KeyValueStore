namespace KeyValueStore.Domain.Security;

public static class EntryAccessPolicy
{
    public static bool IsAllowed(
        UserAccount user,
        string requestedKeyspace,
        EntryOperation operation)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!user.IsActive)
            return false;

        if (string.IsNullOrWhiteSpace(requestedKeyspace))
            return false;

        if (!string.Equals(
                user.Keyspace,
                requestedKeyspace,
                StringComparison.Ordinal))
        {
            return false;
        }

        return user.Role switch
        {
            UserRole.Reader =>
                operation == EntryOperation.Read,

            UserRole.Writer or UserRole.Admin =>
                operation is EntryOperation.Read
                    or EntryOperation.Write
                    or EntryOperation.Delete,

            _ => false
        };
    }
}