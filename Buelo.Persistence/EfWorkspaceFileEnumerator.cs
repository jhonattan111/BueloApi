using Buelo.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Buelo.Persistence;

/// <summary>EF Core <see cref="IWorkspaceFileEnumerator"/> — yields validatable workspace files from the database.</summary>
public sealed class EfWorkspaceFileEnumerator(IDbContextFactory<BueloDbContext> factory) : IWorkspaceFileEnumerator
{
    private static readonly HashSet<string> ValidatableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".buelo", ".json", ".csx", ".cs"
    };

    public async IAsyncEnumerable<WorkspaceFile> EnumerateAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var files = await db.WorkspaceItems.Where(i => !i.IsFolder).ToListAsync();

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.Path);
            if (!ValidatableExtensions.Contains(ext))
                continue;

            yield return new WorkspaceFile(file.Path, ext, file.Content);
        }
    }
}
