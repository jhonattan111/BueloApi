using Buelo.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Buelo.Persistence;

/// <summary>
/// EF Core <see cref="IDefinitionStore"/>. Each definition is a row keyed by (kind, name); the
/// YAML/JSON payload lives in a text column. A singleton over <see cref="IDbContextFactory{T}"/> —
/// every call opens and disposes its own short-lived context, so it is safe to inject into the
/// singleton engine services.
/// </summary>
public sealed class EfDefinitionStore(IDbContextFactory<BueloDbContext> factory) : IDefinitionStore
{
    public async Task<string?> ReadAsync(string kind, string name, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Definitions.FindAsync([kind, name], cancellationToken);
        return entity?.Content;
    }

    public async Task SaveAsync(string kind, string name, string yaml, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var entity = await db.Definitions.FindAsync([kind, name], cancellationToken);
        if (entity is null)
        {
            db.Definitions.Add(new DefinitionEntity
            {
                Kind = kind,
                Name = name,
                Content = yaml,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            entity.Content = yaml;
            entity.UpdatedAt = now;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListAsync(string kind, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Definitions
            .Where(d => d.Kind == kind)
            .Select(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(string kind, string name, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Definitions.FindAsync([kind, name], cancellationToken);
        if (entity is null)
            return false;
        db.Definitions.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
