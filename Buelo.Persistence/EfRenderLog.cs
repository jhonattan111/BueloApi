using Buelo.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Buelo.Persistence;

/// <summary>EF Core <see cref="IRenderLog"/> (writes/reads render history). Singleton over <see cref="IDbContextFactory{T}"/>.</summary>
public sealed class EfRenderLog(IDbContextFactory<BueloDbContext> factory) : IRenderLog
{
    public async Task LogAsync(RenderEvent renderEvent, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.RenderEvents.Add(renderEvent);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RenderEvent>> RecentAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.RenderEvents
            .OrderByDescending(e => e.Id)
            .Take(Math.Clamp(count, 1, 500))
            .ToListAsync(cancellationToken);
    }
}
