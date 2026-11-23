using KeyValueStore.Infrastructure.Storage.Persistent;
using Xunit;

namespace KeyValueStore.Infrastructure.Tests;

public sealed class AppendOnlyFileStoreTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        $"KeyValueStoreLogTests-{Guid.NewGuid():N}");

    private string StorePath =>
        Path.Combine(_testDirectory, "store.log");

    [Fact]
    public void Append_ShouldStoreRecordsAtDifferentOffsets()
    {
        var store = new AppendOnlyFileStore(StorePath);
        var now = DateTimeOffset.UtcNow;

        var setRecord = new LogRecord(
            LogOperation.Set,
            "userA",
            "name",
            new StoredEntry(
                "userA",
                "name",
                "Ali",
                now,
                now,
                null,
                new Dictionary<string, string>
                {
                    ["department"] = "engineering"
                }));

        var deleteRecord = new LogRecord(
            LogOperation.Delete,
            "userA",
            "name",
            null);

        long firstOffset = store.Append(setRecord);
        long secondOffset = store.Append(deleteRecord);

        Assert.Equal(0, firstOffset);
        Assert.True(secondOffset > firstOffset);

        var first = store.ReadAt(firstOffset);
        var second = store.ReadAt(secondOffset);

        Assert.Equal(LogOperation.Set, first.Operation);
        Assert.NotNull(first.Entry);
        Assert.Equal("Ali", first.Entry.Value);
        Assert.Equal(
            "engineering",
            first.Entry.Metadata["department"]);

        Assert.Equal(LogOperation.Delete, second.Operation);
        Assert.Null(second.Entry);
    }

    [Fact]
    public void ReadAt_ShouldWorkWithNewStoreInstance()
    {
        var store = new AppendOnlyFileStore(StorePath);

        var record = new LogRecord(
            LogOperation.Delete,
            "userA",
            "old-key",
            null);

        long offset = store.Append(record);

        var reopenedStore = new AppendOnlyFileStore(StorePath);

        var restored = reopenedStore.ReadAt(offset);

        Assert.Equal(LogOperation.Delete, restored.Operation);
        Assert.Equal("old-key", restored.Key);
    }

    [Fact]
    public void ReadAt_ShouldRejectIncompleteRecord()
    {
        var store = new AppendOnlyFileStore(StorePath);

        long offset = store.Append(new LogRecord(
            LogOperation.Delete,
            "userA",
            "name",
            null));

        using (var stream = new FileStream(
            StorePath,
            FileMode.Open,
            FileAccess.Write))
        {
            stream.SetLength(stream.Length - 2);
        }

        Assert.Throws<InvalidDataException>(
            () => store.ReadAt(offset));
    }

    [Fact]
    public void Replay_ShouldReadRecordsInOrder()
    {
        var store = new AppendOnlyFileStore(StorePath);

        var first = new LogRecord(
            LogOperation.Delete,
            "userA",
            "first",
            null);

        var second = new LogRecord(
            LogOperation.Delete,
            "userA",
            "second",
            null);

        long firstOffset = store.Append(first);
        long secondOffset = store.Append(second);

        var replayed = new List<(long Offset, LogRecord Record)>();

        store.Replay((offset, record) =>
            replayed.Add((offset, record)));

        Assert.Equal(2, replayed.Count);

        Assert.Equal(firstOffset, replayed[0].Offset);
        Assert.Equal("first", replayed[0].Record.Key);

        Assert.Equal(secondOffset, replayed[1].Offset);
        Assert.Equal("second", replayed[1].Record.Key);
    }

    [Fact]
    public void Replay_ShouldRejectIncompleteTrailingRecord()
    {
        var store = new AppendOnlyFileStore(StorePath);

        store.Append(new LogRecord(
            LogOperation.Delete,
            "userA",
            "name",
            null));

        using (var stream = new FileStream(
            StorePath,
            FileMode.Append,
            FileAccess.Write))
        {
            stream.WriteByte(0x01);
        }

        Assert.Throws<InvalidDataException>(
            () => store.Replay((_, _) => { }));
    }

    [Fact]
    public void Recovery_ShouldDoNothingForValidLog()
    {
        var store = new AppendOnlyFileStore(StorePath);

        store.Append(new LogRecord(
            LogOperation.Delete,
            "userA",
            "name",
            null));

        long originalLength = new FileInfo(StorePath).Length;

        string? backupPath = store.RecoverIncompleteTail();

        Assert.Null(backupPath);
        Assert.Equal(
            originalLength,
            new FileInfo(StorePath).Length);
    }

    [Fact]
    public void Recovery_ShouldRepairIncompleteHeader()
    {
        var store = new AppendOnlyFileStore(StorePath);

        store.Append(new LogRecord(
            LogOperation.Delete,
            "userA",
            "valid",
            null));

        long validLength = new FileInfo(StorePath).Length;

        using (var stream = new FileStream(
            StorePath,
            FileMode.Append,
            FileAccess.Write))
        {
            stream.WriteByte(0x01);
        }

        string? backupPath = store.RecoverIncompleteTail();

        Assert.NotNull(backupPath);
        Assert.True(File.Exists(backupPath));
        Assert.Equal(
            new byte[] { 0x01 },
            File.ReadAllBytes(backupPath));

        Assert.Equal(
            validLength,
            new FileInfo(StorePath).Length);

        var replayed = new List<LogRecord>();

        store.Replay((_, record) => replayed.Add(record));

        Assert.Equal("valid", Assert.Single(replayed).Key);
    }

    [Fact]
    public void Recovery_ShouldPreserveValidRecordsAndBackupTail()
    {
        var store = new AppendOnlyFileStore(StorePath);

        store.Append(new LogRecord(
            LogOperation.Delete,
            "userA",
            "first",
            null));

        store.Append(new LogRecord(
            LogOperation.Delete,
            "userA",
            "second",
            null));

        long brokenOffset = store.Append(new LogRecord(
            LogOperation.Delete,
            "userA",
            "incomplete",
            null));

        using (var stream = new FileStream(
            StorePath,
            FileMode.Open,
            FileAccess.Write))
        {
            stream.SetLength(stream.Length - 2);
        }

        byte[] originalBytes = File.ReadAllBytes(StorePath);

        string? backupPath = store.RecoverIncompleteTail();

        Assert.NotNull(backupPath);
        Assert.True(File.Exists(backupPath));

        byte[] expectedTail = originalBytes[(int)brokenOffset..];

        Assert.Equal(
            expectedTail,
            File.ReadAllBytes(backupPath));

        Assert.Equal(
            brokenOffset,
            new FileInfo(StorePath).Length);

        var keys = new List<string>();

        store.Replay((_, record) => keys.Add(record.Key));

        Assert.Equal(new[] { "first", "second" }, keys);

        Assert.Null(store.RecoverIncompleteTail());
    }

    [Fact]
    public void Recovery_ShouldRejectCorruptedCompleteRecord()
    {
        var store = new AppendOnlyFileStore(StorePath);

        long offset = store.Append(new LogRecord(
            LogOperation.Delete,
            "userA",
            "name",
            null));

        long originalLength = new FileInfo(StorePath).Length;

        using (var stream = new FileStream(
            StorePath,
            FileMode.Open,
            FileAccess.Write))
        {
            stream.Position = offset + sizeof(int);
            stream.WriteByte((byte)'!');
        }

        Assert.Throws<InvalidDataException>(
            () => store.RecoverIncompleteTail());

        Assert.Equal(
            originalLength,
            new FileInfo(StorePath).Length);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }
}
