
using KeyValueStore.Application.Abstractions;
using KeyValueStore.Application.UseCases;
using KeyValueStore.Infrastructure.Storage.InMemory;

namespace KeyValueStore.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKeyValueStoreServices(
        this IServiceCollection services)
    {
        services.AddSingleton<
            IKeyValueEntryRepository,
            InMemoryKeyValueEntryRepository>();

        services.AddTransient<SetEntryUseCase>();
        services.AddTransient<GetEntryUseCase>();
        services.AddTransient<DeleteEntryUseCase>();

        return services;
    }
}
