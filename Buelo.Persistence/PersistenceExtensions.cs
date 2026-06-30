using Buelo.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Buelo.Persistence;

public static class PersistenceExtensions
{
    /// <summary>
    /// Registers the database-backed persistence for a self-hosted Buelo instance and makes it the
    /// active store for definitions, the workspace, C# templates, global artefacts and the render
    /// log — replacing the in-memory / file-system defaults from <c>AddBueloEngine()</c> (call this
    /// <em>after</em> it).
    /// <para>
    /// Provider via config <c>Buelo:Database:Provider</c>: <c>sqlite</c> (default — single file,
    /// zero server) or <c>postgres</c> (managed backups, scale-out). Connection string via
    /// <c>Buelo:Database:ConnectionString</c>. One entity model serves both providers.
    /// </para>
    /// <para>
    /// All stores are singletons backed by a pooled-free <see cref="IDbContextFactory{TContext}"/>,
    /// so each operation gets its own short-lived context. That keeps them injectable into the
    /// singleton engine services without a captive-dependency.
    /// </para>
    /// </summary>
    public static IServiceCollection AddBueloPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = (configuration["Buelo:Database:Provider"] ?? "sqlite").Trim().ToLowerInvariant();
        var connectionString = configuration["Buelo:Database:ConnectionString"];

        services.AddDbContextFactory<BueloDbContext>(options =>
        {
            if (provider is "postgres" or "postgresql" or "npgsql")
                // Npgsql migrations live in their own assembly (loaded via Buelo.Api's reference).
                options.UseNpgsql(connectionString ?? "Host=localhost;Database=buelo",
                    npgsql => npgsql.MigrationsAssembly("Buelo.Persistence.Postgres"));
            else
                // SQLite migrations live here in Buelo.Persistence (the default assembly).
                options.UseSqlite(connectionString ?? "Data Source=buelo.db");
        });

        // Database becomes the source of truth for all durable content (overrides AddBueloEngine defaults).
        services.Replace(ServiceDescriptor.Singleton<IDefinitionStore, EfDefinitionStore>());
        services.Replace(ServiceDescriptor.Singleton<IWorkspaceStore, EfWorkspaceStore>());
        services.Replace(ServiceDescriptor.Singleton<IWorkspaceFileEnumerator, EfWorkspaceFileEnumerator>());
        services.Replace(ServiceDescriptor.Singleton<ITemplateStore, EfTemplateStore>());
        services.Replace(ServiceDescriptor.Singleton<IGlobalArtefactStore, EfGlobalArtefactStore>());
        services.Replace(ServiceDescriptor.Singleton<IRenderLog, EfRenderLog>());

        return services;
    }

    /// <summary>
    /// Brings the database schema up to date at startup. Both providers ship migrations (SQLite in
    /// <c>Buelo.Persistence</c>, Postgres in <c>Buelo.Persistence.Postgres</c>), applied with
    /// <c>Migrate()</c>; if the active provider's migrations assembly isn't present it falls back to
    /// <c>EnsureCreated()</c>. Idempotent — safe to call on every boot.
    /// </summary>
    public static void EnsureBueloDatabase(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BueloDbContext>>();
        using var db = factory.CreateDbContext();

        if (db.Database.GetMigrations().Any())
            db.Database.Migrate();
        else
            db.Database.EnsureCreated();
    }
}
