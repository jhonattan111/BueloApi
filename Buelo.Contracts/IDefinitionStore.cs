namespace Buelo.Contracts;

/// <summary>
/// Stores declarative definitions (report, component, styles, formats, lib, validator, theme) as
/// YAML, keyed by kind + name. The pluggable persistence boundary for definitions (blueprint §13);
/// the default implementation is file-system backed (git-friendly).
/// </summary>
public interface IDefinitionStore
{
    /// <summary>Returns the YAML for a definition, or null if it does not exist.</summary>
    Task<string?> ReadAsync(string kind, string name, CancellationToken cancellationToken = default);

    /// <summary>Creates or overwrites a definition's YAML.</summary>
    Task SaveAsync(string kind, string name, string yaml, CancellationToken cancellationToken = default);

    /// <summary>Lists the names of all definitions of a given kind.</summary>
    Task<IReadOnlyList<string>> ListAsync(string kind, CancellationToken cancellationToken = default);

    /// <summary>Deletes a definition. Returns false if it did not exist.</summary>
    Task<bool> DeleteAsync(string kind, string name, CancellationToken cancellationToken = default);
}
