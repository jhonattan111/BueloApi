using System.Text.RegularExpressions;
using Buelo.Contracts;
using Buelo.Engine.Declarative.Expressions;
using Buelo.Engine.Declarative.Modules;

namespace Buelo.Engine.Declarative;

/// <summary>
/// Default <see cref="IValidatorExtensions"/>: named C# validator delegates registered in-process
/// (self-hosted ⇒ trusted). Empty unless the host registers extensions.
/// </summary>
public sealed class ValidatorExtensions : IValidatorExtensions
{
    private readonly Dictionary<string, Func<object?, bool>> _validators = new(StringComparer.OrdinalIgnoreCase);

    public ValidatorExtensions Register(string reference, Func<object?, bool> validate)
    {
        _validators[reference] = validate;
        return this;
    }

    public bool Contains(string reference) => _validators.ContainsKey(reference);

    public bool Validate(string reference, object? value)
        => _validators.TryGetValue(reference, out var fn) && fn(value);
}

/// <summary>
/// Evaluates a declarative validator (blueprint §8) over a value, escalating across three tiers:
/// declarative rules (format/digits/regex/checksum), a pure expression, or a C# extension reference.
/// </summary>
public sealed class DeclarativeValidator
{
    public DataValidationResult Validate(
        ValidatorModule validator, object? value, ExpressionContext context, IValidatorExtensions extensions)
    {
        var errors = new List<string>();

        if (!string.IsNullOrWhiteSpace(validator.Ref))
        {
            // Tier 3 — registered C# extension.
            if (!extensions.Contains(validator.Ref!))
                errors.Add($"Validator extension '{validator.Ref}' is not registered.");
            else if (!extensions.Validate(validator.Ref!, value))
                errors.Add($"Value failed validator '{validator.Name}'.");
        }
        else if (!string.IsNullOrWhiteSpace(validator.Expr))
        {
            // Tier 2 — pure expression; the value is bound to the first declared param (default 'value').
            var paramName = validator.Params.FirstOrDefault() ?? "value";
            var scope = new Dictionary<string, object?> { [paramName] = value };
            if (!ExpressionValues.Truthy(ExpressionEvaluator.Evaluate(validator.Expr!, scope, context)))
                errors.Add($"Value failed validator '{validator.Name}'.");
        }
        else
        {
            // Tier 1 — declarative rules.
            ValidateDeclarative(validator, value, errors);
        }

        return new DataValidationResult { Valid = errors.Count == 0, Errors = errors };
    }

    private static void ValidateDeclarative(ValidatorModule validator, object? value, List<string> errors)
    {
        var text = value?.ToString() ?? string.Empty;
        var digits = new string(text.Where(char.IsDigit).ToArray());

        if (!string.IsNullOrWhiteSpace(validator.Format))
        {
            var expected = validator.Format!.Count(c => c == '#');
            if (digits.Length != expected)
                errors.Add($"Expected {expected} digits but found {digits.Length}.");
        }

        foreach (var rule in validator.Rules)
        {
            if (rule.Digits is { } count && digits.Length != count)
                errors.Add($"Expected {count} digits but found {digits.Length}.");

            if (!string.IsNullOrWhiteSpace(rule.Regex) && !Regex.IsMatch(text, rule.Regex!))
                errors.Add($"Value does not match pattern '{rule.Regex}'.");

            if (rule.Checksum is { } checksum && !IsChecksumValid(digits, checksum))
                errors.Add($"Checksum ({checksum.Scheme}) is invalid.");
        }
    }

    /// <summary>Validates a single mod-11 check digit positioned right after the weighted body.</summary>
    private static bool IsChecksumValid(string digits, ChecksumRule checksum)
    {
        if (!string.Equals(checksum.Scheme, "mod11", StringComparison.OrdinalIgnoreCase))
            return false;

        var weights = checksum.Weights;
        if (weights.Count == 0 || digits.Length <= weights.Count)
            return false;

        var sum = 0;
        for (var i = 0; i < weights.Count; i++)
            sum += weights[i] * (digits[i] - '0');

        var remainder = sum % 11;
        var expected = remainder < 2 ? 0 : 11 - remainder;
        return digits[weights.Count] - '0' == expected;
    }
}
