using System.Text.Json.Serialization;
using Cinema.Api.Endpoints;
using Cinema.Application;
using Cinema.Application.Seats;
using Cinema.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Servicios (composición de dependencias) ----------

var connectionString = builder.Configuration.GetConnectionString("Cinema")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:Cinema'.");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

// Reloj del servidor, inyectable: los tests lo reemplazan por un FakeTimeProvider (ADR-004).
builder.Services.AddSingleton(TimeProvider.System);

// TTL configurable, validado al arrancar: un valor inválido impide levantar la API en lugar de fallar en un request.
builder.Services.AddOptions<SeatSelectionOptions>()
    .Bind(builder.Configuration.GetSection(SeatSelectionOptions.SectionName))
    .Validate(options => options.Ttl > TimeSpan.Zero, "SeatSelection:Ttl debe ser positivo.")
    .ValidateOnStart();

// Errores sin cuerpo (400 de binding, 404 de ruta inexistente) y excepciones no controladas (500) también salen como ProblemDetails.
builder.Services.AddProblemDetails();

// Enums como texto en JSON: "Selected" en lugar de 1.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

var app = builder.Build();

// ---------- Pipeline HTTP ----------

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    // En Development, un error de binding (ej. header X-Hold-Token ausente o que no es un GUID) se lanza como
    // BadHttpRequestException en lugar de responder 400 directamente. Sin esto, el handler lo convertiría en 500.
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError,
    // Un request mal formado es culpa del cliente: no se registra como error del servidor.
    SuppressDiagnosticsCallback = context => context.Exception is BadHttpRequestException,
});
app.UseStatusCodePages();

app.MapOpenApi();                 // documento OpenAPI en /openapi/v1.json
app.MapScalarApiReference();      // UI interactiva en /scalar
app.MapGet("/", () => TypedResults.Redirect("/scalar")).ExcludeFromDescription();

app.MapSeatEndpoints();

await app.Services.MigrateDatabaseAsync();

await app.RunAsync();
