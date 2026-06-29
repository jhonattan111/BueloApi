namespace Buelo.Contracts;

/// <summary>Request to validate a value against a named declarative validator.</summary>
public class DataValidationRequest
{
    /// <summary>The validator name to apply.</summary>
    public string Validator { get; set; } = string.Empty;

    /// <summary>The value to validate.</summary>
    public object? Value { get; set; }

    /// <summary>Module definitions (must include the validator) as YAML documents.</summary>
    public List<string>? Modules { get; set; }
}
