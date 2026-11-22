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

    [Fact]
    public void Constructor_ShouldCopyMetadata()
    {
        var metadata = new Dictionary<string, string>
        {
            ["department"] = "engineering"
        };

        var entry = new KeyValueEntry(
            "name",
            "Ali",
            DateTimeOffset.UnixEpoch,
            metadata: metadata);

        metadata["department"] = "finance";

        Assert.Equal("engineering", entry.Metadata["department"]);
    }

    [Fact]
    public void Constructor_ShouldCreateEmptyMetadataByDefault()
    {
        var entry = new KeyValueEntry(
            "name",
            "Ali",
            DateTimeOffset.UnixEpoch);

        Assert.Empty(entry.Metadata);
    }

    [Fact]
    public void UpdateMetadata_ShouldReplacePreviousMetadata()
    {
        var entry = new KeyValueEntry(
            "name",
            "Ali",
            DateTimeOffset.UnixEpoch,
            metadata: new Dictionary<string, string>
            {
                ["department"] = "engineering",
                ["environment"] = "development"
            });

        entry.UpdateMetadata(new Dictionary<string, string>
        {
            ["department"] = "finance"
        });

        Assert.Single(entry.Metadata);
        Assert.Equal("finance", entry.Metadata["department"]);
        Assert.False(entry.Metadata.ContainsKey("environment"));
    }

    [Fact]
    public void UpdateMetadata_ShouldClearMetadataWhenNull()
    {
        var entry = new KeyValueEntry(
            "name",
            "Ali",
            DateTimeOffset.UnixEpoch,
            metadata: new Dictionary<string, string>
            {
                ["department"] = "engineering"
            });

        entry.UpdateMetadata(null);

        Assert.Empty(entry.Metadata);
    }

    [Fact]
    public void UpdateMetadata_ShouldRejectEmptyKeyWithoutChangingState()
    {
        var entry = new KeyValueEntry(
            "name",
            "Ali",
            DateTimeOffset.UnixEpoch,
            metadata: new Dictionary<string, string>
            {
                ["department"] = "engineering"
            });

        var invalidMetadata = new Dictionary<string, string>
        {
            ["valid"] = "value",
            [" "] = "invalid"
        };

        Assert.Throws<ArgumentException>(
            () => entry.UpdateMetadata(invalidMetadata));

        Assert.Single(entry.Metadata);
        Assert.Equal("engineering", entry.Metadata["department"]);
    }

    [Fact]
    public void UpdateMetadata_ShouldRejectNullValueWithoutChangingState()
    {
        var entry = new KeyValueEntry(
            "name",
            "Ali",
            DateTimeOffset.UnixEpoch,
            metadata: new Dictionary<string, string>
            {
                ["department"] = "engineering"
            });

        var invalidMetadata = new Dictionary<string, string>
        {
            ["department"] = null!
        };

        Assert.Throws<ArgumentException>(
            () => entry.UpdateMetadata(invalidMetadata));

        Assert.Equal("engineering", entry.Metadata["department"]);
    }
}