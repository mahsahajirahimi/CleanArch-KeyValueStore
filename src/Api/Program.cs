using KeyValueStore.Api.Configuration;
using KeyValueStore.Api.Extensions;
using KeyValueStore.Application.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var storageOptions = StorageOptions.FromConfiguration(
    builder.Configuration,
    builder.Environment.ContentRootPath);

builder.Services.AddKeyValueStoreServices(storageOptions);

var app = builder.Build();

app.Services.GetRequiredService<IKeyValueEntryRepository>();

app.MapControllers();

app.Run();