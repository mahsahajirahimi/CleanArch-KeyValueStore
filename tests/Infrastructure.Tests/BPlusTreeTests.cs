using KeyValueStore.Infrastructure.Storage.Persistent;
using Xunit;

namespace KeyValueStore.Infrastructure.Tests;

public sealed class BPlusTreeTests
{
    [Fact]
    public void FindOffset_ShouldReturnNullForMissingKey()
    {
        var tree = new BPlusTree();

        Assert.Null(tree.FindOffset("userA", "missing"));
    }

    [Fact]
    public void Upsert_ShouldInsertAndUpdateOffset()
    {
        var tree = new BPlusTree();

        tree.Upsert("userA", "name", 100);
        Assert.Equal(100L, tree.FindOffset("userA", "name"));

        tree.Upsert("userA", "name", 250);
        Assert.Equal(250L, tree.FindOffset("userA", "name"));
    }

    [Fact]
    public void Insert_ShouldHandleLeafAndInternalSplits()
    {
        var tree = new BPlusTree();

        for (int i = 0; i < 50; i++)
        {
            tree.Upsert("userA", $"key-{i:D3}", i * 100L);
        }

        for (int i = 0; i < 50; i++)
        {
            Assert.Equal(
                i * 100L,
                tree.FindOffset("userA", $"key-{i:D3}"));
        }
    }

    [Fact]
        public void FindOffset_ShouldRespectKeyspaceIsolation()
        {
            var tree = new BPlusTree();

            tree.Upsert("userA", "name", 100);
            tree.Upsert("userB", "name", 200);

            Assert.Equal(100L, tree.FindOffset("userA", "name"));
            Assert.Equal(200L, tree.FindOffset("userB", "name"));
        }


    [Fact]
    public void Delete_ShouldReturnFalseForMissingKey()
    {
        var tree = new BPlusTree();

        Assert.False(tree.Delete("userA", "missing"));

        tree.Upsert("userA", "name", 100);

        Assert.False(tree.Delete("userB", "name"));
        Assert.Equal(100L, tree.FindOffset("userA", "name"));
    }

    [Fact]
    public void Delete_ShouldMergeLeavesAndShrinkRoot()
    {
        var tree = new BPlusTree();

        tree.Upsert("userA", "a", 100);
        tree.Upsert("userA", "b", 200);
        tree.Upsert("userA", "c", 300);
        tree.Upsert("userA", "d", 400);

        Assert.True(tree.Delete("userA", "a"));

        Assert.Null(tree.FindOffset("userA", "a"));
        Assert.Equal(200L, tree.FindOffset("userA", "b"));
        Assert.Equal(300L, tree.FindOffset("userA", "c"));
        Assert.Equal(400L, tree.FindOffset("userA", "d"));
    }

    [Fact]
    public void Delete_ShouldBorrowFromSibling()
    {
        var tree = new BPlusTree();

        foreach (string key in new[] { "b", "c", "d", "e", "a" })
            tree.Upsert("userA", key, 100);

        Assert.True(tree.Delete("userA", "d"));

        Assert.Null(tree.FindOffset("userA", "d"));

        foreach (string key in new[] { "a", "b", "c", "e" })
            Assert.Equal(100L, tree.FindOffset("userA", key));
    }

    [Fact]
    public void Delete_ShouldHandleCascadingRebalancing()
    {
        var tree = new BPlusTree();

        for (int i = 0; i < 120; i++)
            tree.Upsert("userA", $"key-{i:D3}", i * 100L);

        for (int i = 0; i < 120; i += 2)
            Assert.True(tree.Delete("userA", $"key-{i:D3}"));

        for (int i = 0; i < 120; i++)
        {
            long? actual = tree.FindOffset(
                "userA", $"key-{i:D3}");

            if (i % 2 == 0)
                Assert.Null(actual);
            else
                Assert.Equal(i * 100L, actual);
        }

        for (int i = 1; i < 120; i += 2)
            Assert.True(tree.Delete("userA", $"key-{i:D3}"));

        Assert.Null(tree.FindOffset("userA", "key-001"));

        tree.Upsert("userA", "new-key", 999);

        Assert.Equal(
            999L,
            tree.FindOffset("userA", "new-key"));
    }

}
