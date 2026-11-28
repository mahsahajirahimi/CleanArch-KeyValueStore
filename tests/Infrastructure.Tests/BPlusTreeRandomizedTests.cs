using KeyValueStore.Infrastructure.Storage.Persistent;
using Xunit;

namespace KeyValueStore.Infrastructure.Tests;

public sealed class BPlusTreeRandomizedTests
{
    private static readonly string[] Keyspaces =
    {
        "team-a",
        "team-a-extended",
        "team-b",
        "team-c"
    };

    private const int KeysPerKeyspace = 128;

    [Theory]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(2026)]
    [InlineData(20261128)]
    public void RandomizedOperations_ShouldMatchDictionary(
        int seed)
    {
        const int operationCount = 6000;

        var random = new Random(seed);
        var tree = new BPlusTree();

        var model =
            new Dictionary<(string Keyspace, string Key), long>();

        for (int step = 0; step < operationCount; step++)
        {
            string keyspace =
                Keyspaces[random.Next(Keyspaces.Length)];

            string key =
                KeyAt(random.Next(KeysPerKeyspace));

            var address = (keyspace, key);

            int operation = random.Next(100);

            if (operation < 55)
            {
                long offset = step * 53L + 1;

                tree.Upsert(keyspace, key, offset);
                model[address] = offset;
            }
            else if (operation < 85)
            {
                bool expected = model.Remove(address);
                bool actual = tree.Delete(keyspace, key);

                Assert.Equal(expected, actual);
            }
            else
            {
                long? expected =
                    model.TryGetValue(address, out long offset)
                        ? offset
                        : null;

                Assert.Equal(
                    expected,
                    tree.FindOffset(keyspace, key));
            }

            if (step % 101 == 0 ||
                step == operationCount - 1)
            {
                AssertMatchesModel(
                    tree,
                    model,
                    Keyspaces,
                    KeysPerKeyspace,
                    $"seed={seed}, step={step}");
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OrderedMassDeletion_ShouldKeepTreeValid(
        bool ascending)
    {
        const int keyCount = 384;
        const string keyspace = "team-a";

        var tree = new BPlusTree();

        var model =
            new Dictionary<(string Keyspace, string Key), long>();

        for (int i = 0; i < keyCount; i++)
        {
            string key = KeyAt(i);
            long offset = i * 29L;

            tree.Upsert(keyspace, key, offset);
            model[(keyspace, key)] = offset;
        }

        AssertMatchesModel(
            tree,
            model,
            new[] { keyspace },
            keyCount,
            "before mass deletion");

        for (int step = 0; step < keyCount; step++)
        {
            int index = ascending
                ? step
                : keyCount - step - 1;

            string key = KeyAt(index);

            Assert.True(tree.Delete(keyspace, key));
            Assert.True(model.Remove((keyspace, key)));

            if (step % 13 == 0 ||
                step == keyCount - 1)
            {
                AssertMatchesModel(
                    tree,
                    model,
                    new[] { keyspace },
                    keyCount,
                    $"ascending={ascending}, deleted={step + 1}");
            }
        }

        Assert.Empty(model);

        tree.Upsert(keyspace, KeyAt(0), 999_999L);

        Assert.Equal(
            999_999L,
            tree.FindOffset(keyspace, KeyAt(0)));

        Assert.False(
            tree.Delete(keyspace, KeyAt(1)));
    }

    [Fact]
    public void ShuffledInsertDeleteAndReinsert_ShouldMatchDictionary()
    {
        const int keyCount = 600;

        var random = new Random(7331);
        var tree = new BPlusTree();

        var model =
            new Dictionary<(string Keyspace, string Key), long>();

        int[] order = Enumerable.Range(0, keyCount).ToArray();

        Shuffle(order, random);

        foreach (int index in order)
        {
            string keyspace = KeyspaceAt(index);
            string key = KeyAt(index);

            long offset = index * 17L;

            tree.Upsert(keyspace, key, offset);
            model[(keyspace, key)] = offset;
        }

        AssertMatchesModel(
            tree,
            model,
            Keyspaces,
            keyCount,
            "after shuffled insertion");

        Shuffle(order, random);

        for (int step = 0; step < order.Length; step++)
        {
            int index = order[step];

            string keyspace = KeyspaceAt(index);
            string key = KeyAt(index);

            Assert.True(tree.Delete(keyspace, key));
            Assert.True(model.Remove((keyspace, key)));

            if (step % 31 == 0 ||
                step == order.Length - 1)
            {
                AssertMatchesModel(
                    tree,
                    model,
                    Keyspaces,
                    keyCount,
                    $"after shuffled deletion step={step}");
            }
        }

        Assert.Empty(model);

        Shuffle(order, random);

        foreach (int index in order)
        {
            string keyspace = KeyspaceAt(index);
            string key = KeyAt(index);

            long offset = 1_000_000L + index;

            tree.Upsert(keyspace, key, offset);
            model[(keyspace, key)] = offset;
        }

        AssertMatchesModel(
            tree,
            model,
            Keyspaces,
            keyCount,
            "after reinsertion");
    }

    [Fact]
    public void NegativeOffset_ShouldNotModifyExistingEntry()
    {
        var tree = new BPlusTree();

        tree.Upsert("team-a", "name", 100);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            tree.Upsert("team-a", "name", -1));

        Assert.Equal(
            100L,
            tree.FindOffset("team-a", "name"));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            tree.Upsert("team-a", "new", -10));

        Assert.Null(
            tree.FindOffset("team-a", "new"));
    }

    private static void AssertMatchesModel(
        BPlusTree tree,
        IReadOnlyDictionary<
            (string Keyspace, string Key), long> model,
        IReadOnlyList<string> keyspaces,
        int keyCount,
        string context)
    {
        foreach (string keyspace in keyspaces)
        {
            for (int index = 0; index < keyCount; index++)
            {
                string key = KeyAt(index);

                long? expected =
                    model.TryGetValue(
                        (keyspace, key),
                        out long offset)
                        ? offset
                        : null;

                long? actual =
                    tree.FindOffset(keyspace, key);

                Assert.True(
                    actual == expected,
                    $"{context}: Keyspace='{keyspace}', " +
                    $"Key='{key}', " +
                    $"Expected={expected?.ToString() ?? "null"}, " +
                    $"Actual={actual?.ToString() ?? "null"}");
            }
        }
    }

    private static string KeyAt(int index)
    {
        return $"key-{index:D4}";
    }

    private static string KeyspaceAt(int index)
    {
        return Keyspaces[index % Keyspaces.Length];
    }

    private static void Shuffle<T>(
        T[] items,
        Random random)
    {
        for (int i = items.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);

            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
