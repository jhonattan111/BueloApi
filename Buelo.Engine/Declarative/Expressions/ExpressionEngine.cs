using System.Collections;
using System.Globalization;

namespace Buelo.Engine.Declarative.Expressions;

/// <summary>Value coercions shared by the evaluator and the stdlib (weakly-typed, JSON-friendly).</summary>
internal static class ExpressionValues
{
    public static bool Truthy(object? value) => value switch
    {
        null => false,
        bool b => b,
        string s => s.Length > 0,
        _ when IsNumber(value) => ToDouble(value) != 0,
        _ => true,
    };

    public static bool IsNumber(object? value)
        => value is double or float or decimal or long or int or short or byte or sbyte or uint or ulong or ushort;

    public static double ToDouble(object? value) => value switch
    {
        null => 0,
        double d => d,
        bool b => b ? 1 : 0,
        string s => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : 0,
        IConvertible c => c.ToDouble(CultureInfo.InvariantCulture),
        _ => 0,
    };

    public static string Stringify(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        bool b => b ? "true" : "false",
        double d => d.ToString(CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>Enumerates a list value. Strings are scalar (not iterated char-by-char); non-enumerables yield empty.</summary>
    public static IEnumerable<object?> AsEnumerable(object? value)
    {
        switch (value)
        {
            case null or string:
                yield break;
            case IEnumerable enumerable:
                foreach (var item in enumerable) yield return item;
                break;
        }
    }

    /// <summary>
    /// Exposes an object's members as a scope (used per-row and per-item). ExpandoObject (from
    /// JSON) implements IDictionary&lt;string, object&gt;, which is the same runtime type as the
    /// nullable variant, so a single check covers both.
    /// </summary>
    public static IDictionary<string, object?> ToScope(object? value)
        => value as IDictionary<string, object?> ?? new Dictionary<string, object?>();

    public static object? GetMember(object? target, string name)
        => target is IDictionary<string, object?> dict && dict.TryGetValue(name, out var v) ? v : null;

    public static object? GetIndex(object? target, object? index)
    {
        if (target is IList list)
        {
            var i = (int)ToDouble(index);
            return i >= 0 && i < list.Count ? list[i] : null;
        }
        return null;
    }
}

/// <summary>
/// Evaluates an <see cref="Expr"/> against a scope and an <see cref="ExpressionContext"/>.
/// Aggregations (sum/avg/count/min/max) are handled specially: their second argument is a string
/// expression re-evaluated per item. Lib expressions resolve via <c>libName.exprName</c>.
/// </summary>
internal static class ExpressionRuntime
{
    private static readonly HashSet<string> Aggregations = ["sum", "avg", "count", "min", "max"];

    public static object? Eval(Expr expr, IDictionary<string, object?> scope, ExpressionContext ctx) => expr switch
    {
        LiteralExpr literal => literal.Value,
        IdentifierExpr id => scope.TryGetValue(id.Name, out var v) ? v : null,
        MemberExpr member => EvalMember(member, scope, ctx),
        IndexExpr index => ExpressionValues.GetIndex(Eval(index.Target, scope, ctx), Eval(index.Index, scope, ctx)),
        UnaryExpr unary => EvalUnary(unary, scope, ctx),
        TernaryExpr ternary => ExpressionValues.Truthy(Eval(ternary.Condition, scope, ctx))
            ? Eval(ternary.WhenTrue, scope, ctx)
            : Eval(ternary.WhenFalse, scope, ctx),
        BinaryExpr binary => EvalBinary(binary, scope, ctx),
        CallExpr call => EvalCall(call, scope, ctx),
        _ => null,
    };

    private static object? EvalMember(MemberExpr member, IDictionary<string, object?> scope, ExpressionContext ctx)
    {
        // Lib resolution: `vendas.precoFinal` — when `vendas` isn't a scope value, treat it as a lib
        // module and evaluate its named expression in the current scope (item/data available).
        if (member.Target is IdentifierExpr id && !scope.ContainsKey(id.Name)
            && ctx.TryGetLibExpr(id.Name, member.Name, out var libExpr))
            return Eval(ExpressionParser.Parse(libExpr), scope, ctx);

        return ExpressionValues.GetMember(Eval(member.Target, scope, ctx), member.Name);
    }

    private static object? EvalUnary(UnaryExpr unary, IDictionary<string, object?> scope, ExpressionContext ctx)
    {
        var value = Eval(unary.Operand, scope, ctx);
        return unary.Op switch
        {
            "!" => !ExpressionValues.Truthy(value),
            "-" => -ExpressionValues.ToDouble(value),
            _ => null,
        };
    }

    private static object? EvalBinary(BinaryExpr binary, IDictionary<string, object?> scope, ExpressionContext ctx)
    {
        // Short-circuiting / null-aware operators evaluate the right side lazily.
        switch (binary.Op)
        {
            case "&&": return ExpressionValues.Truthy(Eval(binary.Left, scope, ctx)) && ExpressionValues.Truthy(Eval(binary.Right, scope, ctx));
            case "||": return ExpressionValues.Truthy(Eval(binary.Left, scope, ctx)) || ExpressionValues.Truthy(Eval(binary.Right, scope, ctx));
            case "??": return Eval(binary.Left, scope, ctx) ?? Eval(binary.Right, scope, ctx);
        }

        var left = Eval(binary.Left, scope, ctx);
        var right = Eval(binary.Right, scope, ctx);

        return binary.Op switch
        {
            "+" => Add(left, right),
            "-" => ExpressionValues.ToDouble(left) - ExpressionValues.ToDouble(right),
            "*" => ExpressionValues.ToDouble(left) * ExpressionValues.ToDouble(right),
            "/" => ExpressionValues.ToDouble(left) / ExpressionValues.ToDouble(right),
            "%" => ExpressionValues.ToDouble(left) % ExpressionValues.ToDouble(right),
            "==" => AreEqual(left, right),
            "!=" => !AreEqual(left, right),
            "<" => Compare(left, right) < 0,
            "<=" => Compare(left, right) <= 0,
            ">" => Compare(left, right) > 0,
            ">=" => Compare(left, right) >= 0,
            _ => null,
        };
    }

    private static object Add(object? left, object? right)
    {
        if (ExpressionValues.IsNumber(left) && ExpressionValues.IsNumber(right))
            return ExpressionValues.ToDouble(left) + ExpressionValues.ToDouble(right);
        if (left is string || right is string)
            return ExpressionValues.Stringify(left) + ExpressionValues.Stringify(right);
        return ExpressionValues.ToDouble(left) + ExpressionValues.ToDouble(right);
    }

    private static bool AreEqual(object? left, object? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        if (ExpressionValues.IsNumber(left) && ExpressionValues.IsNumber(right))
            return ExpressionValues.ToDouble(left) == ExpressionValues.ToDouble(right);
        return left.Equals(right);
    }

    private static int Compare(object? left, object? right)
    {
        if (ExpressionValues.IsNumber(left) && ExpressionValues.IsNumber(right))
            return ExpressionValues.ToDouble(left).CompareTo(ExpressionValues.ToDouble(right));
        return string.CompareOrdinal(ExpressionValues.Stringify(left), ExpressionValues.Stringify(right));
    }

    private static object? EvalCall(CallExpr call, IDictionary<string, object?> scope, ExpressionContext ctx)
    {
        if (Aggregations.Contains(call.Name))
            return EvalAggregate(call, scope, ctx);

        var args = new object?[call.Args.Count];
        for (var i = 0; i < args.Length; i++)
            args[i] = Eval(call.Args[i], scope, ctx);

        return ExpressionFunctions.Invoke(call.Name, args, ctx);
    }

    private static object EvalAggregate(CallExpr call, IDictionary<string, object?> scope, ExpressionContext ctx)
    {
        if (call.Args.Count == 0)
            throw new FormatException($"'{call.Name}' requires a list argument.");

        var items = ExpressionValues.AsEnumerable(Eval(call.Args[0], scope, ctx)).ToList();

        if (call.Name == "count")
            return (double)items.Count;

        // Optional second argument: a string expression evaluated per item.
        Expr? perItem = call.Args.Count > 1
            ? ExpressionParser.Parse(ExpressionValues.Stringify(Eval(call.Args[1], scope, ctx)))
            : null;

        double ValueOf(object? item) => perItem is null
            ? ExpressionValues.ToDouble(item)
            : ExpressionValues.ToDouble(Eval(perItem, ExpressionValues.ToScope(item), ctx));

        if (items.Count == 0)
            return 0d;

        return call.Name switch
        {
            "sum" => items.Sum(ValueOf),
            "avg" => items.Average(ValueOf),
            "min" => items.Min(ValueOf),
            "max" => items.Max(ValueOf),
            _ => 0d,
        };
    }
}
