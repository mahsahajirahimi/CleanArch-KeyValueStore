using KeyValueStore.Application.Abstractions;
using KeyValueStore.Application.UseCases;
using KeyValueStore.Infrastructure.Storage.InMemory;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<
    IKeyValueEntryRepository,
    InMemoryKeyValueEntryRepository>();

builder.Services.AddTransient<SetEntryUseCase>();
builder.Services.AddTransient<GetEntryUseCase>();
builder.Services.AddTransient<DeleteEntryUseCase>();

var app = builder.Build();

app.MapPut(
    "/api/keyspaces/{keyspace}/entries/{key}",
    (string keyspace, string key, SetEntryRequest request,
     SetEntryUseCase useCase) =>
    {
        try
        {
            useCase.Execute(
                keyspace,
                key,
                request.Value,
                DateTimeOffset.UtcNow);

            return Results.NoContent();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });

app.MapGet(
    "/api/keyspaces/{keyspace}/entries/{key}",
    (string keyspace, string key, GetEntryUseCase useCase) =>
    {
        try
        {
            var entry = useCase.Execute(keyspace, key);

            if (entry is null)
                return Results.NotFound();

            return Results.Ok(new
            {
                entry.Key,
                entry.Value,
                entry.CreatedAt,
                entry.UpdatedAt,
                entry.ExpiresAt
            });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });

app.MapDelete(
    "/api/keyspaces/{keyspace}/entries/{key}",
    (string keyspace, string key, DeleteEntryUseCase useCase) =>
    {
        try
        {
            bool deleted = useCase.Execute(keyspace, key);

            if (!deleted)
                return Results.NotFound();

            return Results.NoContent();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });

app.Run();

public record SetEntryRequest(string Value);