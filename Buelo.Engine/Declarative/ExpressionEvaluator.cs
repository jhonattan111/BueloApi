using System.Text.RegularExpressions;
using Buelo.Engine.Declarative.Expressions;

namespace Buelo.Engine.Declarative;

/// <summary>
/// Public facade over the expression engine (lexer → parser → evaluator under
/// <see cref="Expressions"/>). Resolves <c>{{ }}</c> tokens against a scope, optionally with an
/// <see cref="ExpressionContext"/> carrying lib/format modules. The language is deliberately simple
/// — pure, single-line, side-effect-free (blueprint §6).
/// </summary>
public static partial class ExpressionEvaluator
{
    [GeneratedRegex(@"\{\{(.+?)\}\}", RegexOptions.Singleline)]
    private static partial Regex TokenRegex();

    public static string Interpolate(string? template, IDictionary<string, object?> scope)
        => Interpolate(template, scope, ExpressionContext.Empty);

    /// <summary>Replaces every <c>{{ expr }}</c> token in <paramref name="template"/> with its string value.</summary>
    public static string Interpolate(string? template, IDictionary<string, object?> scope, ExpressionContext context)
    {
        if (string.IsNullOrEmpty(template))
            return template ?? string.Empty;

        return TokenRegex().Replace(template, match =>
            ExpressionValues.Stringify(Evaluate(match.Groups[1].Value.Trim(), scope, context)));
    }

    public static object? Evaluate(string expression, IDictionary<string, object?> scope)
        => Evaluate(expression, scope, ExpressionContext.Empty);

    /// <summary>Parses and evaluates a single expression against the scope and context.</summary>
    public static object? Evaluate(string expression, IDictionary<string, object?> scope, ExpressionContext context)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return null;

        try
        {
            return ExpressionRuntime.Eval(ExpressionParser.Parse(expression), scope, context);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"Invalid expression '{expression}': {ex.Message}", ex);
        }
    }
}
