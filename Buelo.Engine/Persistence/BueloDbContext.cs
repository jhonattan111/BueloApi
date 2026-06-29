using Buelo.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Buelo.Engine.Persistence;

/// <summary>
/// Operational store (render history, audit, logs — append-heavy, §13). Provider chosen by config:
/// SQLite for dev/light self-host, PostgreSQL for prod. One entity model for all providers; the
/// schema stays provider-agnostic so the swap is free.
/// </summary>
public sealed class BueloDbContext(DbContextOptions<BueloDbContext> options) : DbContext(options)
{
    public DbSet<RenderEvent> RenderEvents => Set<RenderEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var renderEvent = modelBuilder.Entity<RenderEvent>();
        renderEvent.HasKey(e => e.Id);
        renderEvent.Property(e => e.ReportName).HasMaxLength(200);
        renderEvent.Property(e => e.Engine).HasMaxLength(40);
        renderEvent.Property(e => e.Format).HasMaxLength(20);
        renderEvent.HasIndex(e => e.CreatedAt);
    }
}
