using Buelo.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Buelo.Persistence;

/// <summary>
/// EF Core <see cref="IWorkspaceStore"/>. The workspace is a flat set of nodes keyed by path; the
/// tree is reconstructed on read and ancestor folders are synthesized from descendant paths (so an
/// explicit row is only needed for an empty folder). Mutating operations load the (small) node set
/// and apply changes in memory with ordinal path matching, keeping behavior identical across
/// SQLite and Postgres. Singleton over <see cref="IDbContextFactory{T}"/>.
/// </summary>
public sealed class EfWorkspaceStore(IDbContextFactory<BueloDbContext> factory) : IWorkspaceStore
{
    public async Task<IReadOnlyList<WorkspaceNode>> GetTreeAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var items = await db.WorkspaceItems.ToListAsync();
        return BuildTree(items);
    }

    public async Task<IReadOnlyList<WorkspaceFileRecord>> ListFilesAsync(string? extension = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var wantedExt = string.IsNullOrWhiteSpace(extension) ? null : NormalizeExtension(extension);

        var files = (await db.WorkspaceItems.Where(i => !i.IsFolder).ToListAsync())
            .Where(i => wantedExt is null ||
                        string.Equals(Path.GetExtension(i.Path), wantedExt, StringComparison.OrdinalIgnoreCase))
            .Select(ToRecord)
            .OrderBy(r => r.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return files;
    }

    public async Task<WorkspaceFileRecord?> GetFileAsync(string path)
    {
        var normalized = WorkspacePath.Normalize(path);
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.WorkspaceItems.FindAsync(normalized);
        return entity is null || entity.IsFolder ? null : ToRecord(entity);
    }

    public async Task<WorkspaceFileRecord> CreateFileAsync(string path, string content = "", bool overwrite = false)
    {
        var normalized = WorkspacePath.Normalize(path);
        await using var db = await factory.CreateDbContextAsync();
        var items = await db.WorkspaceItems.ToListAsync();

        if (IsFolderPath(items, normalized))
            throw new InvalidOperationException($"Path '{normalized}' points to an existing folder.");

        var existing = items.FirstOrDefault(i => i.Path == normalized);
        var now = DateTimeOffset.UtcNow;

        if (existing is not null)
        {
            if (!overwrite)
                throw new InvalidOperationException($"File '{normalized}' already exists.");
            existing.Content = content;
            existing.LastModifiedUtc = now;
        }
        else
        {
            db.WorkspaceItems.Add(new WorkspaceItemEntity
            {
                Path = normalized,
                IsFolder = false,
                Content = content,
                LastModifiedUtc = now
            });
        }

        await db.SaveChangesAsync();
        return new WorkspaceFileRecord
        {
            Path = normalized,
            Name = NameOf(normalized),
            Extension = Path.GetExtension(normalized),
            Content = content,
            LastModifiedUtc = now
        };
    }

    public async Task<WorkspaceFileRecord> UpdateFileAsync(string path, string content, bool createIfMissing = false)
    {
        var normalized = WorkspacePath.Normalize(path);
        await using var db = await factory.CreateDbContextAsync();
        var now = DateTimeOffset.UtcNow;

        var entity = await db.WorkspaceItems.FindAsync(normalized);
        if (entity is null || entity.IsFolder)
        {
            if (entity is { IsFolder: true })
                throw new InvalidOperationException($"Path '{normalized}' points to an existing folder.");
            if (!createIfMissing)
                throw new FileNotFoundException($"File '{normalized}' not found.", normalized);

            db.WorkspaceItems.Add(new WorkspaceItemEntity
            {
                Path = normalized,
                IsFolder = false,
                Content = content,
                LastModifiedUtc = now
            });
        }
        else
        {
            entity.Content = content;
            entity.LastModifiedUtc = now;
        }

        await db.SaveChangesAsync();
        return new WorkspaceFileRecord
        {
            Path = normalized,
            Name = NameOf(normalized),
            Extension = Path.GetExtension(normalized),
            Content = content,
            LastModifiedUtc = now
        };
    }

    public async Task CreateFolderAsync(string path)
    {
        var normalized = WorkspacePath.Normalize(path);
        if (string.IsNullOrEmpty(normalized))
            return;

        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.WorkspaceItems.FindAsync(normalized);
        if (existing is { IsFolder: false })
            throw new InvalidOperationException($"Path '{normalized}' points to an existing file.");

        if (existing is null)
        {
            db.WorkspaceItems.Add(new WorkspaceItemEntity
            {
                Path = normalized,
                IsFolder = true,
                Content = string.Empty,
                LastModifiedUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }

    public async Task MoveAsync(string path, string destinationPath, bool overwrite = false)
    {
        var source = WorkspacePath.Normalize(path);
        var destination = WorkspacePath.Normalize(destinationPath);
        if (string.IsNullOrEmpty(source))
            throw new InvalidOperationException("Source path must not be empty.");

        await using var db = await factory.CreateDbContextAsync();
        var items = await db.WorkspaceItems.ToListAsync();

        var sourcePrefix = source + "/";
        var moving = items.Where(i => i.Path == source || i.Path.StartsWith(sourcePrefix, StringComparison.Ordinal)).ToList();
        if (moving.Count == 0)
            throw new FileNotFoundException($"Source '{source}' not found.", source);

        var destPrefix = destination + "/";
        var collisions = items.Where(i => i.Path == destination || i.Path.StartsWith(destPrefix, StringComparison.Ordinal)).ToList();
        if (collisions.Count > 0)
        {
            if (!overwrite)
                throw new InvalidOperationException($"Destination '{destination}' already exists.");
            db.WorkspaceItems.RemoveRange(collisions);
        }

        db.WorkspaceItems.RemoveRange(moving);
        var now = DateTimeOffset.UtcNow;
        foreach (var item in moving)
        {
            var newPath = item.Path == source ? destination : destination + item.Path[source.Length..];
            db.WorkspaceItems.Add(new WorkspaceItemEntity
            {
                Path = newPath,
                IsFolder = item.IsFolder,
                Content = item.Content,
                LastModifiedUtc = now
            });
        }

        await db.SaveChangesAsync();
    }

    public Task RenameAsync(string path, string newName, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new InvalidOperationException("New name must not be empty.");

        var normalized = WorkspacePath.Normalize(path);
        var parent = WorkspacePath.Parent(normalized);
        var destination = string.IsNullOrEmpty(parent) ? newName.Trim() : $"{parent}/{newName.Trim()}";
        return MoveAsync(normalized, destination, overwrite);
    }

    public async Task DeleteAsync(string path, bool recursive = true)
    {
        var normalized = WorkspacePath.Normalize(path);
        await using var db = await factory.CreateDbContextAsync();
        var items = await db.WorkspaceItems.ToListAsync();

        var prefix = normalized + "/";
        var matched = items.Where(i => i.Path == normalized || i.Path.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        if (matched.Count == 0)
            throw new FileNotFoundException($"Path '{normalized}' not found.", normalized);

        if (!recursive && matched.Any(i => i.Path != normalized))
            throw new InvalidOperationException($"Folder '{normalized}' is not empty.");

        db.WorkspaceItems.RemoveRange(matched);
        await db.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(string path)
    {
        var normalized = WorkspacePath.Normalize(path);
        if (string.IsNullOrEmpty(normalized))
            return false;

        await using var db = await factory.CreateDbContextAsync();
        if (await db.WorkspaceItems.FindAsync(normalized) is not null)
            return true;

        var prefix = normalized + "/";
        return (await db.WorkspaceItems.Select(i => i.Path).ToListAsync())
            .Any(p => p.StartsWith(prefix, StringComparison.Ordinal));
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static bool IsFolderPath(IReadOnlyCollection<WorkspaceItemEntity> items, string path)
    {
        if (items.Any(i => i.Path == path && i.IsFolder))
            return true;
        var prefix = path + "/";
        return items.Any(i => i.Path.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static List<WorkspaceNode> BuildTree(IReadOnlyCollection<WorkspaceItemEntity> items)
    {
        // Folder paths = explicit folder rows + every ancestor directory of every item.
        var folderPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item.IsFolder)
                folderPaths.Add(item.Path);
            foreach (var ancestor in WorkspacePath.Ancestors(item.Path))
                folderPaths.Add(ancestor);
        }

        var folderNodes = folderPaths.ToDictionary(
            p => p,
            p => new WorkspaceNode
            {
                Path = p,
                Name = NameOf(p),
                Type = "folder",
                Kind = "folder",
                Extension = string.Empty,
                Children = []
            },
            StringComparer.Ordinal);

        var roots = new List<WorkspaceNode>();

        void Attach(WorkspaceNode node, string nodePath)
        {
            var parent = WorkspacePath.Parent(nodePath);
            if (string.IsNullOrEmpty(parent))
                roots.Add(node);
            else if (folderNodes.TryGetValue(parent, out var parentNode))
                parentNode.Children.Add(node);
            else
                roots.Add(node); // defensive: orphan with no folder row
        }

        foreach (var (folderPath, node) in folderNodes.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            Attach(node, folderPath);

        foreach (var file in items.Where(i => !i.IsFolder))
        {
            var node = new WorkspaceNode
            {
                Path = file.Path,
                Name = NameOf(file.Path),
                Type = "file",
                Extension = Path.GetExtension(file.Path),
                Kind = InferKind(Path.GetExtension(file.Path)),
                Children = []
            };
            Attach(node, file.Path);
        }

        SortLevel(roots);
        return roots;
    }

    private static void SortLevel(List<WorkspaceNode> nodes)
    {
        nodes.Sort((a, b) =>
        {
            var byType = (a.Type == "folder" ? 0 : 1).CompareTo(b.Type == "folder" ? 0 : 1);
            return byType != 0 ? byType : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        foreach (var node in nodes)
            if (node.Children is List<WorkspaceNode> children)
                SortLevel(children);
    }

    private static WorkspaceFileRecord ToRecord(WorkspaceItemEntity entity) => new()
    {
        Path = entity.Path,
        Name = NameOf(entity.Path),
        Extension = Path.GetExtension(entity.Path),
        Content = entity.Content,
        LastModifiedUtc = entity.LastModifiedUtc
    };

    private static string NameOf(string path)
    {
        var idx = path.LastIndexOf('/');
        return idx < 0 ? path : path[(idx + 1)..];
    }

    private static string InferKind(string extension) => extension.ToLowerInvariant() switch
    {
        ".buelo" => "report",
        ".json" => "data",
        ".cs" => "helper",
        ".csx" => "helper",
        _ => "file"
    };

    private static string NormalizeExtension(string extension)
    {
        var value = extension.Trim();
        return value.StartsWith('.') ? value : "." + value;
    }
}
