using Cinema.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Cinema")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:Cinema'.");

builder.Services.AddInfrastructure(connectionString);

var app = builder.Build();

await app.Services.MigrateDatabaseAsync();

app.MapGet("/", () => "Hello World!");

app.Run();
