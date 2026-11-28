using KeyValueStore.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace KeyValueStore.Api.Tests;

public sealed class StorageOptionsTests
{
    [Fact]
    public void MissingMode_ShouldDefaultToInMemory()
    {
        StorageOptions options = ReadOptions(
            new Dictionary<string, string?>());

        Assert.Equal("InMemory", options.Mode);
        Assert.Null(options.FilePath);
        Assert.Equal(100, options.CacheCapacity);
    }

    [Theory]
    [InlineData("InMemory")]
    [InlineData("inmemory")]
    [InlineData("INMEMORY")]
    [InlineData(" InMemory ")]
    public void InMemoryMode_ShouldBeCaseInsensitive(
        string mode)
    {
        StorageOptions options = ReadOptions(
            new Dictionary<string, string?>
            {
                ["Storage:Mode"] = mode,
                ["Storage:CacheCapacity"] = "8"
            });

        Assert.Equal(
            "InMemory",
            options.Mode,
            ignoreCase: true);

        Assert.Null(options.FilePath);
        Assert.Equal(8, options.CacheCapacity);
    }

    [Theory]
    [InlineData("Persistent")]
    [InlineData("persistent")]
    [InlineData("PERSISTENT")]
    [InlineData(" Persistent ")]
    public void PersistentMode_ShouldResolveRelativePath(
        string mode)
    {
        string contentRoot = Path.Combine(
            Path.GetTempPath(),
            "KvsConfigurationTests");

        StorageOptions options = ReadOptions(
            new Dictionary<string, string?>
            {
                ["Storage:Mode"] = mode,
                ["Storage:FilePath"] = "data/store.log",
                ["Storage:CacheCapacity"] = "16"
            },
            contentRoot);

        string expectedPath = Path.GetFullPath(
            "data/store.log",
            Path.GetFullPath(contentRoot));

        Assert.Equal(
            "Persistent",
            options.Mode,
            ignoreCase: true);

        Assert.Equal(expectedPath, options.FilePath);
        Assert.Equal(16, options.CacheCapacity);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData("   ")]
    public void InvalidMode_ShouldBeRejected(string mode)
    {
        var config = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["Storage:Mode"] = mode
            });

        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                StorageOptions.FromConfiguration(
                    config,
                    Path.GetTempPath()));

        Assert.Contains(
            "Unsupported storage mode",
            exception.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("")]
    public void InvalidCacheCapacity_ShouldBeRejected(
        string capacity)
    {
        var config = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["Storage:Mode"] = "InMemory",
                ["Storage:CacheCapacity"] = capacity
            });

        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                StorageOptions.FromConfiguration(
                    config,
                    Path.GetTempPath()));

        Assert.Contains(
            "Storage:CacheCapacity",
            exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PersistentWithoutFilePath_ShouldBeRejected(
        string? filePath)
    {
        var config = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["Storage:Mode"] = "Persistent",
                ["Storage:FilePath"] = filePath
            });

        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                StorageOptions.FromConfiguration(
                    config,
                    Path.GetTempPath()));

        Assert.Contains(
            "Persistent storage requires a file path",
            exception.Message);
    }

    [Fact]
    public void InMemory_ShouldNotRequireStorageFilePath()
    {
        StorageOptions options = ReadOptions(
            new Dictionary<string, string?>
            {
                ["Storage:Mode"] = "InMemory",
                ["Storage:FilePath"] = " "
            });

        Assert.Null(options.FilePath);
    }

    [Fact]
    public void EmptyContentRoot_ShouldBeRejected()
    {
        var config = CreateConfiguration(
            new Dictionary<string, string?>());

        Assert.Throws<ArgumentException>(() =>
            StorageOptions.FromConfiguration(
                config,
                ""));
    }

    private static StorageOptions ReadOptions(
        Dictionary<string, string?> values,
        string? contentRoot = null)
    {
        IConfiguration config = CreateConfiguration(values);

        return StorageOptions.FromConfiguration(
            config,
            contentRoot ?? Path.GetTempPath());
    }

    private static IConfiguration CreateConfiguration(
        Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}