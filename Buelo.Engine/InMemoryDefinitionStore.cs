using System.Collections.Concurrent;
using Buelo.Contracts;

namespace Buelo.Engine;

/// <summary>In-memory <see cref="IDefinitionStore"/> for dev and tests (no persistence between restarts).</summary>
public sealed class InMemoryDefinitionStore : IDefinitionStore
{
    private readonly ConcurrentDictionary<(string Kind, string Name), string> _store = new();

    public Task<string?> ReadAsync(string kind, string name, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue((kind, name), out var yaml) ? yaml : null);

    public Task SaveAsync(string kind, string name, string yaml, CancellationToken cancellationToken = default)
    {
        _store[(kind, name)] = yaml;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListAsync(string kind, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> names = _store.Keys.Where(k => k.Kind == kind).Select(k => k.Name).ToList();
        return Task.FromResult(names);
    }

    public Task<bool> DeleteAsync(string kind, string name, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryRemove((kind, name), out _));
}
