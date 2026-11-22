
using KeyValueStore.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddKeyValueStoreServices();

var app = builder.Build();

app.MapControllers();

app.Run();