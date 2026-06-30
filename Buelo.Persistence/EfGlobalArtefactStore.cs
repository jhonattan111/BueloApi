using System.Text.Json;
using Buelo.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Buelo.Persistence;

/// <summary>EF Core <see cref="IGlobalArtefactStore"/>. Singleton over <see cref="IDbContextFactory{T}"/>.</summary>
public sealed class EfGlobalArtefactStore(IDbContextFactory<BueloDbContext> factory) : IGlobalArtefactStore
{
    public async Task<GlobalArtefact?> GetAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.GlobalArtefacts.FindAsync(id);
        return entity is null ? null : ToModel(entity);
    }

    public async Task<GlobalArtefact?> GetByNameAsync(string name, string extension)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.GlobalArtefacts
            .FirstOrDefaultAsync(a => a.Name == name && a.Extension == extension);
        return entity is null ? null : ToModel(entity);
    }

    public async Task<IReadOnlyList<GlobalArtefact>> ListAsync(string? extensionFilter = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.GlobalArtefacts.AsQueryable();
        if (extensionFilter is not null)
            query = query.Where(a => a.Extension == extensionFilter);
        var entities = await query.ToListAsync();
        return entities.Select(ToModel).ToList();
    }

    public async Task<GlobalArtefact> SaveAsync(GlobalArtefact artefact)
    {
        await using var db = await factory.CreateDbContextAsync();
        var now = DateTimeOffset.UtcNow;

        var entity = artefact.Id == Guid.Empty ? null : await db.GlobalArtefacts.FindAsync(artefact.Id);
        if (entity is null)
        {
            if (artefact.Id == Guid.Empty)
                artefact.Id = Guid.NewGuid();
            artefact.CreatedAt = now;
            artefact.UpdatedAt = now;
            db.GlobalArtefacts.Add(ToEntity(artefact));
        }
        else
        {
            artefact.CreatedAt = entity.CreatedAt;
            artefact.UpdatedAt = now;
            entity.Name = artefact.Name;
            entity.Extension = artefact.Extension;
            entity.Content = artefact.Content;
            entity.Description = artefact.Description;
            entity.TagsJson = JsonSerializer.Serialize(artefact.Tags);
            entity.UpdatedAt = now;
        }

        await db.SaveChangesAsync();
        return artefact;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.GlobalArtefacts.FindAsync(id);
        if (entity is null)
            return false;
        db.GlobalArtefacts.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    private static GlobalArtefactEntity ToEntity(GlobalArtefact a) => new()
    {
        Id = a.Id,
        Name = a.Name,
        Extension = a.Extension,
        Content = a.Content,
        Description = a.Description,
        TagsJson = JsonSerializer.Serialize(a.Tags),
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt
    };

    private static GlobalArtefact ToModel(GlobalArtefactEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Extension = e.Extension,
        Content = e.Content,
        Description = e.Description,
        Tags = JsonSerializer.Deserialize<List<string>>(e.TagsJson) ?? [],
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };
}
