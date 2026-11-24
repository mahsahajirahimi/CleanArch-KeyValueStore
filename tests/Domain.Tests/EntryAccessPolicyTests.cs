using KeyValueStore.Domain.Security;
using Xunit;

namespace KeyValueStore.Domain.Tests;

public sealed class EntryAccessPolicyTests
{
    [Fact]
    public void Reader_ShouldOnlyBeAllowedToRead()
    {
        var user = new UserAccount(
            Guid.NewGuid(),
            "userA",
            UserRole.Reader);

        Assert.True(EntryAccessPolicy.IsAllowed(
            user, "userA", EntryOperation.Read));

        Assert.False(EntryAccessPolicy.IsAllowed(
            user, "userA", EntryOperation.Write));

        Assert.False(EntryAccessPolicy.IsAllowed(
            user, "userA", EntryOperation.Delete));
    }

    [Fact]
    public void Writer_ShouldBeAllowedToReadWriteAndDelete()
    {
        var user = new UserAccount(
            Guid.NewGuid(),
            "userA",
            UserRole.Writer);

        Assert.True(EntryAccessPolicy.IsAllowed(
            user, "userA", EntryOperation.Read));

        Assert.True(EntryAccessPolicy.IsAllowed(
            user, "userA", EntryOperation.Write));

        Assert.True(EntryAccessPolicy.IsAllowed(
            user, "userA", EntryOperation.Delete));
    }

    [Fact]
    public void Admin_ShouldAccessOwnKeyspace()
    {
        var user = new UserAccount(
            Guid.NewGuid(),
            "userA",
            UserRole.Admin);

        Assert.True(EntryAccessPolicy.IsAllowed(
            user, "userA", EntryOperation.Delete));

        Assert.False(EntryAccessPolicy.IsAllowed(
            user, "userB", EntryOperation.Read));
    }

    [Fact]
    public void User_ShouldNotAccessAnotherKeyspace()
    {
        var user = new UserAccount(
            Guid.NewGuid(),
            "userA",
            UserRole.Writer);

        Assert.False(EntryAccessPolicy.IsAllowed(
            user, "userB", EntryOperation.Read));

        Assert.False(EntryAccessPolicy.IsAllowed(
            user, "userB", EntryOperation.Write));
    }

    [Fact]
    public void DeactivatedUser_ShouldHaveNoAccess()
    {
        var user = new UserAccount(
            Guid.NewGuid(),
            "userA",
            UserRole.Admin);

        user.Deactivate();

        Assert.False(EntryAccessPolicy.IsAllowed(
            user, "userA", EntryOperation.Read));

        Assert.False(EntryAccessPolicy.IsAllowed(
            user, "userA", EntryOperation.Write));
    }

    [Fact]
    public void ChangeRole_ShouldUpdatePermissions()
    {
        var user = new UserAccount(
            Guid.NewGuid(),
            "userA",
            UserRole.Reader);

        Assert.False(EntryAccessPolicy.IsAllowed(
            user, "userA", EntryOperation.Write));

        user.ChangeRole(UserRole.Writer);

        Assert.True(EntryAccessPolicy.IsAllowed(
            user, "userA", EntryOperation.Write));
    }

    [Fact]
    public void UserAccount_ShouldRejectInvalidIdentity()
    {
        Assert.Throws<ArgumentException>(() =>
            new UserAccount(
                Guid.Empty,
                "userA",
                UserRole.Reader));

        Assert.Throws<ArgumentException>(() =>
            new UserAccount(
                Guid.NewGuid(),
                "",
                UserRole.Reader));
    }

    [Fact]
    public void UnknownRoleOrOperation_ShouldBeDenied()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UserAccount(
                Guid.NewGuid(),
                "userA",
                (UserRole)999));

        var user = new UserAccount(
            Guid.NewGuid(),
            "userA",
            UserRole.Admin);

        Assert.False(EntryAccessPolicy.IsAllowed(
            user,
            "userA",
            (EntryOperation)999));
    }
}