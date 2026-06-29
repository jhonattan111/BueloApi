using Buelo.Contracts;

namespace Buelo.Engine;

/// <summary>
/// File-system <see cref="IDefinitionStore"/>: each definition is a <c>.yml</c> file at
/// <c>{root}/{kind}/{name}.yml</c>. Git-friendly (diff/PR/review come for free) and reuses the
/// same on-disk model as the rest of Buelo (blueprint §13, default for definitions).
/// </summary>
public sealed class FileSystemDefinitionStore(string rootPath) : IDefinitionStore
{
    private string DirectoryFor(string kind) => Path.Combine(rootPath, Sanitize(kind));

    private string PathFor(string kind, string name) => Path.Combine(DirectoryFor(kind), Sanitize(name) + ".yml");

    public async Task<string?> ReadAsync(string kind, string name, CancellationToken cancellationToken = default)
    {
        var path = PathFor(kind, name);
        return File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken) : null;
    }

    public async Task SaveAsync(string kind, string name, string yaml, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(DirectoryFor(kind));
        await File.WriteAllTextAsync(PathFor(kind, name), yaml, cancellationToken);
    }

    public Task<IReadOnlyList<string>> ListAsync(string kind, CancellationToken cancellationToken = default)
    {
        var directory = DirectoryFor(kind);
        IReadOnlyList<string> names = Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.yml").Select(Path.GetFileNameWithoutExtension).OfType<string>().ToList()
            : [];
        return Task.FromResult(names);
    }

    public Task<bool> DeleteAsync(string kind, string name, CancellationToken cancellationToken = default)
    {
        var path = PathFor(kind, name);
        if (!File.Exists(path))
            return Task.FromResult(false);
        File.Delete(path);
        return Task.FromResult(true);
    }

    /// <summary>Prevents path traversal: only the bare file/dir name is allowed.</summary>
    private static string Sanitize(string value)
    {
        var name = Path.GetFileName(value.Trim());
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException($"Invalid definition identifier '{value}'.");
        return name;
    }
}
