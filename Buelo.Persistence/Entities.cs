namespace Buelo.Persistence;

/// <summary>
/// A declarative definition row (<c>report</c>/<c>component</c>/<c>styles</c>/<c>theme</c>/<c>formats</c>/
/// <c>lib</c>/<c>validator</c>/<c>data</c>). Keyed by (<see cref="Kind"/>, <see cref="Name"/>); the YAML/JSON
/// payload lives in <see cref="Content"/>. Mirrors the on-disk <c>{kind}/{name}.yml</c> layout.
/// </summary>
internal sealed class DefinitionEntity
{
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// A single workspace node (file or folder), keyed by its workspace-relative <see cref="Path"/>
/// ('/'-separated). Folders carry no <see cref="Content"/>; the tree is reconstructed from the
/// flat set of paths.
/// </summary>
internal sealed class WorkspaceItemEntity
{
    public string Path { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset LastModifiedUtc { get; set; }
}

/// <summary>
/// A persisted C# template. The full <c>TemplateRecord</c> (including dynamic mock data, page
/// settings and artefacts) is stored as a JSON document in <see cref="Json"/> — the same
/// self-contained shape the file-system store writes — with a few scalar columns promoted for
/// cheap listing/ordering.
/// </summary>
internal sealed class TemplateEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Json { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>A point-in-time snapshot of a <see cref="TemplateEntity"/>, serialized in <see cref="Json"/>.</summary>
internal sealed class TemplateVersionEntity
{
    public Guid TemplateId { get; set; }
    public int Version { get; set; }
    public string Json { get; set; } = string.Empty;
    public DateTimeOffset SavedAt { get; set; }
}

/// <summary>A reusable global artefact (mock data, shared snippet, helper). Mapped relationally.</summary>
internal sealed class GlobalArtefactEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Tags serialized as a JSON array (provider-agnostic).</summary>
    public string TagsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
