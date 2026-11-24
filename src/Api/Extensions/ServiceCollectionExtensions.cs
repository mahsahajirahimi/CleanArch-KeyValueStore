using KeyValueStore.Api.Configuration;
using KeyValueStore.Application.Abstractions;
using KeyValueStore.Application.UseCases;
using KeyValueStore.Infrastructure.Storage.InMemory;
using KeyValueStore.Infrastructure.Storage.Persistent;
using Microsoft.Extensions.DependencyInjection;

namespace KeyValueStore.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKeyValueStoreServices(
        this IServiceCollection services,
        StorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        switch (options.Mode.Trim().ToLowerInvariant())
        {
            case "inmemory":
                services.AddSingleton<
                    IKeyValueEntryRepository,
                    InMemoryKeyValueEntryRepository>();
                break;

            case "persistent":
                services.AddSingleton<IKeyValueEntryRepository>(
                    _ => new LogStructuredKeyValueEntryRepository(
                        options.FilePath
                            ?? throw new InvalidOperationException(
                                "Persistent storage requires a file path."),
                        options.CacheCapacity));
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported storage mode: '{options.Mode}'.");
        }

        services.AddTransient<SetEntryUseCase>();
        services.AddTransient<GetEntryUseCase>();
        services.AddTransient<DeleteEntryUseCase>();

        return services;
    }
}
