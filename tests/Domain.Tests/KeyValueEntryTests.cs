using KeyValueStore.Domain.Aggregates;
using Xunit;

namespace KeyValueStore.Domain.Tests;

public class KeyValueEntryTests
{
    [Fact]
    public void Constructor_ShouldSetInitialValues()
    {
        var now = DateTimeOffset.UnixEpoch;
        var entry = new KeyValueEntry("name", "Ali", now);

        Assert.Equal("name", entry.Key);
        Assert.Equal("Ali", entry.Value);
        Assert.Equal(now, entry.CreatedAt);
        Assert.Equal(now, entry.UpdatedAt);
        Assert.False(entry.IsExpired(DateTimeOffset.MaxValue));
    }

    [Fact]
    public void Constructor_ShouldRejectEmptyKey()
    {
        var now = DateTimeOffset.UnixEpoch;

        Assert.Throws<ArgumentException>(
            () => new KeyValueEntry("", "Ali", now));
    }

    [Fact]
    public void UpdateValue_ShouldUpdateValueAndTimestamp()
    {
        var now = DateTimeOffset.UnixEpoch;
        var entry = new KeyValueEntry("name", "Ali", now);
        var later = now.AddMinutes(5);

        entry.UpdateValue("Sara", later);

        Assert.Equal("Sara", entry.Value);
        Assert.Equal(now, entry.CreatedAt);
        Assert.Equal(later, entry.UpdatedAt);
    }

    [Fact]
    public void IsExpired_ShouldRespectExpirationTime()
    {
        var now = DateTimeOffset.UnixEpoch;
        var expiresAt = now.AddMinutes(5);
        var entry = new KeyValueEntry("name", "Ali", now, expiresAt);

        Assert.False(entry.IsExpired(now.AddMinutes(4)));
        Assert.True(entry.IsExpired(now.AddMinutes(5)));
    }

    [Fact]
    public void UpdateValue_ShouldRejectOlderTimestamp()
    {
        var now = DateTimeOffset.UnixEpoch;
        var entry = new KeyValueEntry("name", "Ali", now);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => entry.UpdateValue("Sara", now.AddMinutes(-1)));

        Assert.Equal("Ali", entry.Value);
    }
}