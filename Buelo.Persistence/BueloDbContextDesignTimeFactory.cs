using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Buelo.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build a <see cref="BueloDbContext"/> at design time without booting the
/// app. Migrations are authored against SQLite (the default provider); generate/update them with:
/// <code>dotnet ef migrations add &lt;Name&gt; -p Buelo.Persistence -s Buelo.Persistence</code>
/// </summary>
public sealed class BueloDbContextDesignTimeFactory : IDesignTimeDbContextFactory<BueloDbContext>
{
    public BueloDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BueloDbContext>()
            .UseSqlite("Data Source=buelo.db")
            .Options;
        return new BueloDbContext(options);
    }
}
