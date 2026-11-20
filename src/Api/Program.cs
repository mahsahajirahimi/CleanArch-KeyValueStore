var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "KeyValueStore API");

app.Run();