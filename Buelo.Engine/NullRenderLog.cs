using Buelo.Contracts;

namespace Buelo.Engine;

/// <summary>
/// No-op <see cref="IRenderLog"/> — the default registered by <c>AddBueloEngine()</c> when no
/// database is configured. <c>AddBueloPersistence()</c> (Buelo.Persistence) replaces it with the
/// EF-backed render log.
/// </summary>
public sealed class NullRenderLog : IRenderLog
{
    public Task LogAsync(RenderEvent renderEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<RenderEvent>> RecentAsync(int count = 50, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RenderEvent>>([]);
}
