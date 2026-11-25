using KeyValueStore.Api.Configuration;
using KeyValueStore.Api.Extensions;
using KeyValueStore.Application.Abstractions;
using KeyValueStore.Application.UseCases;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var storageOptions = StorageOptions.FromConfiguration(
    builder.Configuration,
    builder.Environment.ContentRootPath);

builder.Services.AddKeyValueStoreServices(storageOptions);

string securityDataDirectory =
    Path.GetDirectoryName(storageOptions.FilePath)
    ?? Path.Combine(
        builder.Environment.ContentRootPath,
        "data");

builder.Services.AddApiKeyAuthentication(
    securityDataDirectory);

builder.Services.AddTransient<AuthorizeEntryOperationUseCase>();

var app = builder.Build();

app.Services.GetRequiredService<IKeyValueEntryRepository>();
app.Services.GetRequiredService<IUserAccountRepository>();
app.Services.GetRequiredService<IApiKeyCredentialRepository>();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();