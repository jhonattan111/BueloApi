using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Buelo.Persistence.Postgres;

/// <summary>
/// Lets <c>dotnet ef</c> build a <see cref="BueloDbContext"/> against PostgreSQL at design time.
/// Add/refresh the Npgsql migrations with:
/// <code>dotnet ef migrations add &lt;Name&gt; -p Buelo.Persistence.Postgres -s Buelo.Persistence.Postgres</code>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class NpgsqlDesignTimeFactory : IDesignTimeDbContextFactory<BueloDbContext>
{
    public BueloDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BueloDbContext>()
            .UseNpgsql("Host=localhost;Database=buelo",
                npgsql => npgsql.MigrationsAssembly("Buelo.Persistence.Postgres"))
            .Options;
        return new BueloDbContext(options);
    }
}
