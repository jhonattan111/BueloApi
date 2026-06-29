using Buelo.Contracts;
using Buelo.Engine.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Buelo.Tests.Engine;

public class EfRenderLogTests
{
    private static (BueloDbContext Db, SqliteConnection Connection) NewDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<BueloDbContext>().UseSqlite(connection).Options;
        var db = new BueloDbContext(options);
        db.Database.EnsureCreated();
        return (db, connection);
    }

    [Fact]
    public async Task Logs_and_reads_back_recent_newest_first()
    {
        var (db, connection) = NewDatabase();
        try
        {
            var log = new EfRenderLog(db);
            await log.LogAsync(new RenderEvent { ReportName = "fatura", ByteCount = 123, Success = true, CreatedAt = DateTimeOffset.UtcNow });
            await log.LogAsync(new RenderEvent { ReportName = "folha", Success = false, Error = "boom", CreatedAt = DateTimeOffset.UtcNow });

            var recent = await log.RecentAsync(10);

            Assert.Equal(2, recent.Count);
            Assert.Equal("folha", recent[0].ReportName); // newest first
            Assert.False(recent[0].Success);
            Assert.Equal("boom", recent[0].Error);
            Assert.True(recent[1].Success);
        }
        finally
        {
            db.Dispose();
            connection.Dispose();
        }
    }
}
