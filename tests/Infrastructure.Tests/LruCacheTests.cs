using KeyValueStore.Infrastructure.Storage.Caching;
using Xunit;

namespace KeyValueStore.Infrastructure.Tests;

public sealed class LruCacheTests
{
    [Fact]
    public void Constructor_ShouldRejectInvalidCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LruCache<string, int>(0));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LruCache<string, int>(-1));
    }

    [Fact]
    public void Put_ShouldStoreAndRetrieveValue()
    {
        var cache = new LruCache<string, int>(2);

        cache.Put("A", 100);

        Assert.True(cache.TryGet("A", out int value));
        Assert.Equal(100, value);
        Assert.Equal(1, cache.Count);

        Assert.False(cache.TryGet("missing", out _));
    }

    [Fact]
    public void Put_ShouldEvictLeastRecentlyUsedEntry()
    {
        var cache = new LruCache<string, int>(2);

        cache.Put("A", 100);
        cache.Put("B", 200);
        cache.Put("C", 300);

        Assert.False(cache.TryGet("A", out _));

        Assert.True(cache.TryGet("B", out int b));
        Assert.Equal(200, b);

        Assert.True(cache.TryGet("C", out int c));
        Assert.Equal(300, c);

        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void TryGet_ShouldRefreshUsageOrder()
    {
        var cache = new LruCache<string, int>(2);

        cache.Put("A", 100);
        cache.Put("B", 200);

        Assert.True(cache.TryGet("A", out _));

        cache.Put("C", 300);

        Assert.False(cache.TryGet("B", out _));

        Assert.True(cache.TryGet("A", out int a));
        Assert.Equal(100, a);

        Assert.True(cache.TryGet("C", out int c));
        Assert.Equal(300, c);
    }

    [Fact]
    public void Put_ShouldUpdateExistingEntryAndRefreshUsage()
    {
        var cache = new LruCache<string, int>(2);

        cache.Put("A", 100);
        cache.Put("B", 200);

        cache.Put("A", 999);
        cache.Put("C", 300);

        Assert.Equal(2, cache.Count);

        Assert.True(cache.TryGet("A", out int value));
        Assert.Equal(999, value);

        Assert.False(cache.TryGet("B", out _));
        Assert.True(cache.TryGet("C", out _));
    }

    [Fact]
    public void Remove_ShouldRemoveOnlyRequestedEntry()
    {
        var cache = new LruCache<string, int>(2);

        cache.Put("A", 100);
        cache.Put("B", 200);

        Assert.True(cache.Remove("A"));
        Assert.False(cache.Remove("A"));

        Assert.False(cache.TryGet("A", out _));
        Assert.True(cache.TryGet("B", out _));

        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Clear_ShouldRemoveAllEntries()
    {
        var cache = new LruCache<string, int>(2);

        cache.Put("A", 100);
        cache.Put("B", 200);

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.False(cache.TryGet("A", out _));
        Assert.False(cache.TryGet("B", out _));

        cache.Put("C", 300);

        Assert.True(cache.TryGet("C", out int value));
        Assert.Equal(300, value);
    }

    [Fact]
    public void Cache_ShouldDistinguishKeyspaces()
    {
        var cache = new LruCache<
            (string Keyspace, string Key),
            string>(2);

        cache.Put(("userA", "name"), "Ali");
        cache.Put(("userB", "name"), "Sara");

        Assert.True(cache.TryGet(
            ("userA", "name"), out string? valueA));

        Assert.True(cache.TryGet(
            ("userB", "name"), out string? valueB));

        Assert.Equal("Ali", valueA);
        Assert.Equal("Sara", valueB);
    }
}
