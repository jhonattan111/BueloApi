namespace Buelo.Contracts;

/// <summary>
/// Registry of C# validator extensions referenced by tier-3 validators (<c>ref: Ns.Method</c>).
/// Self-hosted ⇒ this code is trusted. Empty by default; instances register named delegates.
/// </summary>
public interface IValidatorExtensions
{
    bool Contains(string reference);
    bool Validate(string reference, object? value);
}

/// <summary>Outcome of validating a value against a declarative validator.</summary>
public sealed class DataValidationResult
{
    public bool Valid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}
