using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace KeyValueStore.Api.Tests;

public sealed class TestApiFactory
    : WebApplicationFactory<Program>
{
    private readonly bool _ownsDataDirectory;
    private readonly string _storageMode;
    private readonly string _cacheCapacity;
    private readonly string? _storageFilePath;

    public string DataDirectory { get; }

    public TestApiFactory(
        string? dataDirectory = null,
        string storageMode = "Persistent",
        string cacheCapacity = "4",
        string? storageFilePath = null)
    {
        _ownsDataDirectory = dataDirectory is null;
        _storageMode = storageMode;
        _cacheCapacity = cacheCapacity;
        _storageFilePath = storageFilePath;

        DataDirectory = dataDirectory is null
            ? Path.Combine(
                Path.GetTempPath(),
                $"KeyValueStoreApiTests-{Guid.NewGuid():N}")
            : Path.GetFullPath(dataDirectory);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Storage:Mode"] = _storageMode,

                    ["Storage:FilePath"] =
                        _storageFilePath
                        ?? Path.Combine(
                            DataDirectory,
                            "store.log"),

                    ["Storage:CacheCapacity"] =
                        _cacheCapacity,

                    ["Security:DataDirectory"] =
                        DataDirectory
                });
        });

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing &&
            _ownsDataDirectory &&
            Directory.Exists(DataDirectory))
        {
            Directory.Delete(
                DataDirectory,
                recursive: true);
        }
    }
}