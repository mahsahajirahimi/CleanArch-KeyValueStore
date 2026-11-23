using KeyValueStore.Api.Configuration;
using KeyValueStore.Application.Abstractions;
using KeyValueStore.Application.UseCases;
using KeyValueStore.Infrastructure.Storage.InMemory;
using KeyValueStore.Infrastructure.Storage.Persistent;

namespace KeyValueStore.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKeyValueStoreServices(
        this IServiceCollection services,
        StorageOptions options)
    {
        switch (options.Mode)
        {
            case "InMemory":
            {
                services.AddSingleton<
                    IKeyValueEntryRepository,
                    InMemoryKeyValueEntryRepository>();

                break;
            }

            case "Persistent":
            {
                string filePath = options.FilePath
                    ?? throw new InvalidOperationException(
                        "File path is required for Persistent mode.");

                services.AddSingleton<IKeyValueEntryRepository>(
                    _ => new PersistentKeyValueEntryRepository(filePath));

                break;
            }

            default:
                throw new InvalidOperationException(
                    $"Unsupported storage mode: {options.Mode}");
        }

        services.AddTransient<SetEntryUseCase>();
        services.AddTransient<GetEntryUseCase>();
        services.AddTransient<DeleteEntryUseCase>();

        return services;
    }
}
