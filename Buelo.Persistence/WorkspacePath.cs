namespace Buelo.Persistence;

/// <summary>
/// Workspace path rules, kept identical to the file-system store so both backends accept and
/// reject exactly the same paths. Paths are '/'-separated, relative, with no traversal segments.
/// </summary>
internal static class WorkspacePath
{
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var trimmed = path.Trim().Replace('\\', '/');
        while (trimmed.StartsWith('/'))
            trimmed = trimmed[1..];

        if (System.IO.Path.IsPathRooted(trimmed))
            throw new InvalidOperationException($"Absolute path is not allowed: '{path}'.");

        var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => s is "." or ".."))
            throw new InvalidOperationException($"Invalid path traversal segment in '{path}'.");

        if (segments.Any(s => s.Contains(':')))
            throw new InvalidOperationException($"Invalid path segment in '{path}'.");

        return string.Join('/', segments);
    }

    public static string Parent(string normalizedPath)
    {
        var idx = normalizedPath.LastIndexOf('/');
        return idx <= 0 ? string.Empty : normalizedPath[..idx];
    }

    /// <summary>Every ancestor folder path of <paramref name="normalizedPath"/>, outermost first (excludes the path itself).</summary>
    public static IEnumerable<string> Ancestors(string normalizedPath)
    {
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var acc = string.Empty;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            acc = acc.Length == 0 ? segments[i] : $"{acc}/{segments[i]}";
            yield return acc;
        }
    }
}
