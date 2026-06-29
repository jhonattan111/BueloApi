using Buelo.Api;
using Buelo.Engine;
using Buelo.Engine.Persistence;
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

// Operational persistence (render history/audit): SQLite (dev) / PostgreSQL (prod) by config.
builder.Services.AddBueloPersistence(builder.Configuration);

builder.Services.AddCors(opt =>
{
    opt.AddPolicy("default",
        policy =>
        {
            policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyMethod()
            .AllowAnyHeader();
        });
});

var app = builder.Build();

// Create the operational schema if missing (dev convenience; prod manages this with migrations).
app.Services.EnsureBueloDatabase();


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

// Optional API-key gate (enabled only when Buelo:ApiKey is configured).
app.UseMiddleware<ApiKeyMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();
