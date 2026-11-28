using KeyValueStore.Infrastructure.Storage;
using Xunit;

namespace KeyValueStore.Infrastructure.Tests;

public sealed class DataDirectoryLockTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"KvsDirectoryLockTests-{Guid.NewGuid():N}");

    [Fact]
    public void Acquire_ShouldRejectSecondOwner()
    {
        using var first = DataDirectoryLock.Acquire(
            _directory);

        Assert.Throws<IOException>(() =>
            DataDirectoryLock.Acquire(_directory));
    }

    [Fact]
    public void Dispose_ShouldAllowAcquiringAgain()
    {
        using (DataDirectoryLock.Acquire(_directory))
        {
            Assert.Throws<IOException>(() =>
                DataDirectoryLock.Acquire(_directory));
        }

        using var second = DataDirectoryLock.Acquire(
            _directory);

        Assert.NotNull(second);
    }

    [Fact]
    public void DifferentDirectories_ShouldAllowIndependentOwners()
    {
        string firstDirectory = Path.Combine(
            _directory,
            "first");

        string secondDirectory = Path.Combine(
            _directory,
            "second");

        using var first = DataDirectoryLock.Acquire(
            firstDirectory);

        using var second = DataDirectoryLock.Acquire(
            secondDirectory);

        Assert.NotNull(first);
        Assert.NotNull(second);
    }

    [Fact]
    public void Acquire_ShouldRejectEmptyDirectory()
    {
        Assert.Throws<ArgumentException>(() =>
            DataDirectoryLock.Acquire(""));

        Assert.Throws<ArgumentException>(() =>
            DataDirectoryLock.Acquire("   "));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(
                _directory,
                recursive: true);
        }
    }
}