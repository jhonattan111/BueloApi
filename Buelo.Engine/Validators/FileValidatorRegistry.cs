using Buelo.Contracts;

namespace Buelo.Engine.Validators;

public class FileValidatorRegistry
{
    private readonly IReadOnlyList<IFileValidator> _validators;

    public FileValidatorRegistry(IEnumerable<IFileValidator> validators)
    {
        _validators = [.. validators];
    }

    public IFileValidator? GetValidator(string extension)
        => _validators.FirstOrDefault(v =>
            v.SupportedExtensions.Any(e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase)));

    public async Task<FileValidationResult> ValidateAsync(string extension, string content)
    {
        var validator = GetValidator(extension);
        if (validator is null)
        {
            // No server-side validator for this extension (e.g. YAML, which the editor validates
            // client-side via monaco-yaml + JSON Schemas). Treat as valid with no diagnostics —
            // emitting a "no validator" warning here is just noise in the UI.
            return new FileValidationResult { Valid = true };
        }

        // CsharpFileValidator supports both .cs and .csx with different parse modes.
        if (validator is CsharpFileValidator csValidator)
            return await csValidator.ValidateWithExtensionAsync(content, extension);

        return await validator.ValidateAsync(content);
    }
}
