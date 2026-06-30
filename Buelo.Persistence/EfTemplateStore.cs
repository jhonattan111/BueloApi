using System.Text.Json;
using System.Text.Json.Serialization;
using Buelo.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Buelo.Persistence;

/// <summary>
/// EF Core <see cref="ITemplateStore"/>. The full <see cref="TemplateRecord"/> is serialized to a
/// JSON document (the same self-contained shape the file-system store uses), so dynamic mock data,
/// page settings and embedded artefacts round-trip without bespoke relational mapping. Keeps up to
/// <see cref="MaxVersionsPerTemplate"/> version snapshots per template, created on each save.
/// Singleton over <see cref="IDbContextFactory{T}"/>.
/// </summary>
public sealed class EfTemplateStore(IDbContextFactory<BueloDbContext> factory) : ITemplateStore
{
    /// <summary>Maximum number of versions retained per template.</summary>
    public const int MaxVersionsPerTemplate = 20;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<TemplateRecord?> GetAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.Templates.FindAsync(id);
        return entity is null ? null : Deserialize(entity.Json);
    }

    public async Task<IEnumerable<TemplateRecord>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.Templates.Select(t => t.Json).ToListAsync();
        return rows.Select(Deserialize).Where(r => r is not null).Select(r => r!).ToList();
    }

    public async Task<TemplateRecord> SaveAsync(TemplateRecord template)
    {
        await using var db = await factory.CreateDbContextAsync();
        var now = DateTimeOffset.UtcNow;

        var existing = template.Id == Guid.Empty ? null : await db.Templates.FindAsync(template.Id);

        if (existing is null)
        {
            if (template.Id == Guid.Empty)
                template.Id = Guid.NewGuid();
            template.CreatedAt = now;
            template.UpdatedAt = now;
            db.Templates.Add(new TemplateEntity
            {
                Id = template.Id,
                Name = template.Name,
                Json = Serialize(template),
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            // Snapshot the stored copy (old content) before overwriting.
            var previous = Deserialize(existing.Json);
            if (previous is not null)
                await AppendVersionAsync(db, template.Id, previous);

            template.CreatedAt = existing.CreatedAt;
            template.UpdatedAt = now;
            existing.Name = template.Name;
            existing.Json = Serialize(template);
            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync();
        return template;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.Templates.FindAsync(id);
        if (entity is null)
            return false;

        var versions = await db.TemplateVersions.Where(v => v.TemplateId == id).ToListAsync();
        db.TemplateVersions.RemoveRange(versions);
        db.Templates.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<TemplateVersion>> GetVersionsAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.TemplateVersions
            .Where(v => v.TemplateId == id)
            .OrderBy(v => v.Version)
            .Select(v => v.Json)
            .ToListAsync();
        return rows
            .Select(j => JsonSerializer.Deserialize<TemplateVersion>(j, JsonOpts))
            .Where(v => v is not null)
            .Select(v => v!)
            .ToList();
    }

    public async Task<TemplateVersion?> GetVersionAsync(Guid id, int version)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.TemplateVersions.FindAsync(id, version);
        return entity is null ? null : JsonSerializer.Deserialize<TemplateVersion>(entity.Json, JsonOpts);
    }

    private static async Task AppendVersionAsync(BueloDbContext db, Guid templateId, TemplateRecord previous)
    {
        var existingVersions = await db.TemplateVersions
            .Where(v => v.TemplateId == templateId)
            .OrderBy(v => v.Version)
            .ToListAsync();

        var nextVersion = existingVersions.Count == 0 ? 1 : existingVersions[^1].Version + 1;

        var snapshot = new TemplateVersion
        {
            Version = nextVersion,
            Template = previous.Template,
            Artefacts = previous.Artefacts.Select(a => new TemplateArtefact
            {
                Path = a.Path,
                Name = a.Name,
                Extension = a.Extension,
                Content = a.Content
            }).ToList(),
            SavedAt = previous.UpdatedAt
        };

        db.TemplateVersions.Add(new TemplateVersionEntity
        {
            TemplateId = templateId,
            Version = nextVersion,
            Json = JsonSerializer.Serialize(snapshot, JsonOpts),
            SavedAt = previous.UpdatedAt
        });

        // Prune to the most recent MaxVersionsPerTemplate snapshots.
        var overflow = existingVersions.Count + 1 - MaxVersionsPerTemplate;
        for (var i = 0; i < overflow && i < existingVersions.Count; i++)
            db.TemplateVersions.Remove(existingVersions[i]);
    }

    private static string Serialize(TemplateRecord record) => JsonSerializer.Serialize(record, JsonOpts);

    private static TemplateRecord? Deserialize(string json) => JsonSerializer.Deserialize<TemplateRecord>(json, JsonOpts);
}
