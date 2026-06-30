using Buelo.Api;
using Buelo.Engine;
using Buelo.Persistence;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddBueloEngine();

// Database-backed persistence for all durable content (definitions, workspace, templates, global
// artefacts, render history): SQLite (default, single-file) / PostgreSQL by config. Replaces the
// in-memory / file-system defaults registered by AddBueloEngine().
builder.Services.AddBueloPersistence(builder.Configuration);

// Allowed CORS origins — configurable for deploys (Buelo:Cors:Origins, comma-separated);
// defaults to the local dev web origin. (With the bundled nginx proxy the web is same-origin,
// so CORS isn't needed there.)
var corsOrigins = (builder.Configuration["Buelo:Cors:Origins"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(opt =>
{
    opt.AddPolicy("default",
        policy =>
        {
            policy
            .WithOrigins(corsOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader();
        });
});

var app = builder.Build();

// Bring the database schema up to date (applies the configured provider's migrations).
app.Services.EnsureBueloDatabase();

// First-run import of the shipped example definitions into the database.
await app.Services.SeedBueloContentFromDiskAsync(builder.Configuration);


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

QuestPDF.Settings.License = LicenseType.Community;

app.UseCors("default");

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Public liveness probe (stays open even when the API key gate is on).
app.MapGet("/ping", () => Results.Ok(new { status = "ok" }));

// Public readiness probe — verifies the database is reachable (used by container health checks).
app.MapGet("/health", async (IDbContextFactory<BueloDbContext> dbFactory, CancellationToken ct) =>
{
    try
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Database.CanConnectAsync(ct)
            ? Results.Ok(new { status = "healthy" })
            : Results.Json(new { status = "unhealthy" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch
    {
        return Results.Json(new { status = "unhealthy" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

// Optional API-key gate (enabled only when Buelo:ApiKey is configured).
app.UseMiddleware<ApiKeyMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();
