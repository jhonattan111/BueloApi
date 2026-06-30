using Buelo.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Buelo.Tests.Persistence;

/// <summary>
/// An <see cref="IDbContextFactory{TContext}"/> over a throwaway SQLite file — exercises the EF
/// stores against the real default provider. Dispose deletes the temp database.
/// </summary>
internal sealed class TestDbFactory : IDbContextFactory<BueloDbContext>, IDisposable
{
    private readonly string _path;
    private readonly DbContextOptions<BueloDbContext> _options;

    public TestDbFactory()
    {
        _path = Path.Combine(Path.GetTempPath(), $"buelo-test-{Guid.NewGuid():N}.db");
        _options = new DbContextOptionsBuilder<BueloDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options;

        using var db = CreateDbContext();
        db.Database.EnsureCreated();
    }

    public BueloDbContext CreateDbContext() => new(_options);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(_path); } catch { /* best effort */ }
    }
}
