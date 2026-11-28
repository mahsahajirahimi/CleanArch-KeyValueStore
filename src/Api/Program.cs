using KeyValueStore.Api.Configuration;
using KeyValueStore.Api.Extensions;
using KeyValueStore.Application.Abstractions;
using KeyValueStore.Application.UseCases;
using KeyValueStore.Infrastructure.Storage;

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

builder.Services.AddSingleton<DataDirectoryLock>(
    _ => DataDirectoryLock.Acquire(
        securityDataDirectory));

builder.Services.AddApiKeyAuthentication(
    securityDataDirectory);

builder.Services.AddTransient<AuthorizeEntryOperationUseCase>();
builder.Services.AddTransient<CreateUserUseCase>();
builder.Services.AddTransient<IssueApiKeyUseCase>();
builder.Services.AddTransient<RevokeApiKeyUseCase>();
builder.Services.AddTransient<RotateApiKeyUseCase>();

var app = builder.Build();

app.Services.GetRequiredService<DataDirectoryLock>();

app.Services.GetRequiredService<IKeyValueEntryRepository>();
app.Services.GetRequiredService<IUserAccountRepository>();
app.Services.GetRequiredService<IApiKeyCredentialRepository>();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }