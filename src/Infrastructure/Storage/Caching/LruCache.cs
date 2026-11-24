using System.Diagnostics.CodeAnalysis;

namespace KeyValueStore.Infrastructure.Storage.Caching;

internal sealed class LruCache<TKey, TValue>
    where TKey : notnull
{
    private readonly Dictionary<
        TKey,
        LinkedListNode<CacheEntry>> _entries = new();

    private readonly LinkedList<CacheEntry> _usage = new();

    private sealed record CacheEntry(TKey Key, TValue Value);

    public int Capacity { get; }

    public int Count => _entries.Count;

    public LruCache(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                "Cache capacity must be positive.");
        }

        Capacity = capacity;
    }

    public bool TryGet(
        TKey key,
        [MaybeNullWhen(false)] out TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!_entries.TryGetValue(key, out var node))
        {
            value = default;
            return false;
        }

        MoveToFront(node);

        value = node.Value.Value;
        return true;
    }

    public void Put(TKey key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (_entries.TryGetValue(key, out var existingNode))
        {
            existingNode.Value = new CacheEntry(key, value);
            MoveToFront(existingNode);
            return;
        }

        var newNode = _usage.AddFirst(
            new CacheEntry(key, value));

        _entries.Add(key, newNode);

        if (_entries.Count > Capacity)
        {
            EvictLeastRecentlyUsed();
        }
    }

    public bool Remove(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!_entries.Remove(key, out var node))
            return false;

        _usage.Remove(node);

        return true;
    }

    public void Clear()
    {
        _entries.Clear();
        _usage.Clear();
    }

    private void MoveToFront(
        LinkedListNode<CacheEntry> node)
    {
        if (node == _usage.First)
            return;

        _usage.Remove(node);
        _usage.AddFirst(node);
    }

    private void EvictLeastRecentlyUsed()
    {
        var lastNode = _usage.Last
            ?? throw new InvalidOperationException(
                "Cannot evict from an empty cache.");

        _entries.Remove(lastNode.Value.Key);
        _usage.RemoveLast();
    }
}
