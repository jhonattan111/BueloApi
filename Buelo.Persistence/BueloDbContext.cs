using Buelo.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Buelo.Persistence;

/// <summary>
/// The Buelo operational + content database. Holds every durable artefact of a self-hosted
/// instance: declarative definitions, the editor workspace, C# templates (with version history),
/// global artefacts, and the append-only render log.
/// <para>
/// One entity model serves all providers; the schema is kept provider-agnostic so switching
/// between <c>sqlite</c> (default, single-file, zero-server) and <c>postgres</c> (managed backups)
/// is a connection-string change. See <see cref="PersistenceExtensions.AddBueloPersistence"/>.
/// </para>
/// </summary>
public sealed class BueloDbContext(DbContextOptions<BueloDbContext> options) : DbContext(options)
{
    internal DbSet<DefinitionEntity> Definitions => Set<DefinitionEntity>();
    internal DbSet<WorkspaceItemEntity> WorkspaceItems => Set<WorkspaceItemEntity>();
    internal DbSet<TemplateEntity> Templates => Set<TemplateEntity>();
    internal DbSet<TemplateVersionEntity> TemplateVersions => Set<TemplateVersionEntity>();
    internal DbSet<GlobalArtefactEntity> GlobalArtefacts => Set<GlobalArtefactEntity>();

    /// <summary>Render history / audit (append-heavy). Mapped from the <see cref="RenderEvent"/> contract.</summary>
    public DbSet<RenderEvent> RenderEvents => Set<RenderEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var definition = modelBuilder.Entity<DefinitionEntity>();
        definition.ToTable("Definitions");
        definition.HasKey(e => new { e.Kind, e.Name });
        definition.Property(e => e.Kind).HasMaxLength(40);
        definition.Property(e => e.Name).HasMaxLength(200);

        var workspaceItem = modelBuilder.Entity<WorkspaceItemEntity>();
        workspaceItem.ToTable("WorkspaceItems");
        workspaceItem.HasKey(e => e.Path);
        workspaceItem.Property(e => e.Path).HasMaxLength(1024);
        workspaceItem.HasIndex(e => e.IsFolder);

        var template = modelBuilder.Entity<TemplateEntity>();
        template.ToTable("Templates");
        template.HasKey(e => e.Id);
        template.Property(e => e.Name).HasMaxLength(200);
        template.HasIndex(e => e.UpdatedAt);

        var templateVersion = modelBuilder.Entity<TemplateVersionEntity>();
        templateVersion.ToTable("TemplateVersions");
        templateVersion.HasKey(e => new { e.TemplateId, e.Version });

        var globalArtefact = modelBuilder.Entity<GlobalArtefactEntity>();
        globalArtefact.ToTable("GlobalArtefacts");
        globalArtefact.HasKey(e => e.Id);
        globalArtefact.Property(e => e.Name).HasMaxLength(200);
        globalArtefact.Property(e => e.Extension).HasMaxLength(40);
        globalArtefact.HasIndex(e => new { e.Name, e.Extension });

        var renderEvent = modelBuilder.Entity<RenderEvent>();
        renderEvent.ToTable("RenderEvents");
        renderEvent.HasKey(e => e.Id);
        renderEvent.Property(e => e.ReportName).HasMaxLength(200);
        renderEvent.Property(e => e.Engine).HasMaxLength(40);
        renderEvent.Property(e => e.Format).HasMaxLength(20);
        renderEvent.HasIndex(e => e.CreatedAt);
    }
}
