using Buelo.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Buelo.Engine.Persistence;

public static class PersistenceExtensions
{
    /// <summary>
    /// Registers the operational EF Core store. Provider by config (<c>Buelo:Database:Provider</c>):
    /// <c>sqlite</c> (default, dev/light self-host) or <c>postgres</c> (prod). Replaces the no-op
    /// <see cref="IRenderLog"/> with the EF-backed one.
    /// </summary>
    public static IServiceCollection AddBueloPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = (configuration["Buelo:Database:Provider"] ?? "sqlite").Trim().ToLowerInvariant();
        var connectionString = configuration["Buelo:Database:ConnectionString"];

        services.AddDbContext<BueloDbContext>(options =>
        {
            if (provider is "postgres" or "postgresql" or "npgsql")
                options.UseNpgsql(connectionString ?? "Host=localhost;Database=buelo");
            else
                options.UseSqlite(connectionString ?? "Data Source=buelo-operational.db");
        });

        services.AddScoped<IRenderLog, EfRenderLog>(); // last registration wins over NullRenderLog
        return services;
    }

    /// <summary>
    /// Brings the operational schema up to date. When migrations are present (prod path), applies them;
    /// otherwise creates the schema directly (dev/self-host convenience). So adding migrations later
    /// switches the app over automatically, no startup change needed.
    /// </summary>
    public static void EnsureBueloDatabase(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BueloDbContext>();
        if (db.Database.GetMigrations().Any())
            db.Database.Migrate();
        else
            db.Database.EnsureCreated();
    }
}
