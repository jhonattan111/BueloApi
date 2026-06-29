using Buelo.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Buelo.Engine.Persistence;

/// <summary>EF Core-backed <see cref="IRenderLog"/> (writes/reads render history).</summary>
public sealed class EfRenderLog(BueloDbContext db) : IRenderLog
{
    public async Task LogAsync(RenderEvent renderEvent, CancellationToken cancellationToken = default)
    {
        db.RenderEvents.Add(renderEvent);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RenderEvent>> RecentAsync(int count = 50, CancellationToken cancellationToken = default)
        => await db.RenderEvents
            .OrderByDescending(e => e.Id)
            .Take(Math.Clamp(count, 1, 500))
            .ToListAsync(cancellationToken);
}

/// <summary>No-op <see cref="IRenderLog"/> — the default when no operational database is configured.</summary>
public sealed class NullRenderLog : IRenderLog
{
    public Task LogAsync(RenderEvent renderEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<RenderEvent>> RecentAsync(int count = 50, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RenderEvent>>([]);
}
