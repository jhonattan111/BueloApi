namespace Buelo.Engine.Declarative.Expressions;

/// <summary>
/// Carries user-defined symbols into the evaluator: <c>lib</c> named expressions (resolved as
/// <c>libName.exprName</c>) and custom <c>formats</c> (used as named mask functions). Empty by default.
/// </summary>
public sealed class ExpressionContext
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> NoLibs
        = new Dictionary<string, IReadOnlyDictionary<string, string>>();
    private static readonly IReadOnlyDictionary<string, string> NoFormats
        = new Dictionary<string, string>();

    public static readonly ExpressionContext Empty = new();

    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _libs;
    private readonly IReadOnlyDictionary<string, string> _formats;

    public ExpressionContext(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? libs = null,
        IReadOnlyDictionary<string, string>? formats = null)
    {
        _libs = libs ?? NoLibs;
        _formats = formats ?? NoFormats;
    }

    public bool TryGetLibExpr(string lib, string name, out string expr)
    {
        expr = string.Empty;
        return _libs.TryGetValue(lib, out var module) && module.TryGetValue(name, out expr!);
    }

    public bool TryGetFormat(string name, out string mask)
        => _formats.TryGetValue(name, out mask!);
}
