using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace KeyValueStore.Api.Tests;

public sealed class TestApiFactory
    : WebApplicationFactory<Program>
{
    public string DataDirectory { get; } = Path.Combine(
        Path.GetTempPath(),
        $"KeyValueStoreApiTests-{Guid.NewGuid():N}");

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Storage:Mode"] = "Persistent",

                    ["Storage:FilePath"] = Path.Combine(
                        DataDirectory,
                        "store.log"),

                    ["Storage:CacheCapacity"] = "4"
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

        if (disposing && Directory.Exists(DataDirectory))
        {
            Directory.Delete(
                DataDirectory,
                recursive: true);
        }
    }
}