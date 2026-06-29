using System.Globalization;
using System.Text;

namespace Buelo.Engine.Declarative.Expressions;

/// <summary>
/// The expression stdlib: pure, deterministic formatting and string helpers (blueprint §6).
/// BR formats are built in. Aggregations (sum/avg/count/min/max) live in the evaluator because
/// they need per-item sub-expression evaluation. Heavy logic belongs in a C# extension, not here.
/// </summary>
internal static class ExpressionFunctions
{
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    public static object? Invoke(string name, object?[] args, ExpressionContext ctx)
    {
        object? Arg(int i) => i < args.Length ? args[i] : null;

        return name switch
        {
            "moeda" => Convert.ToDecimal(ExpressionValues.ToDouble(Arg(0))).ToString("C", Br),
            "percent" => ExpressionValues.ToDouble(Arg(0)).ToString("N2", Br) + "%",
            "data" => FormatDate(Arg(0), Arg(1) as string),
            "cpf" => Mask(Digits(Arg(0)), "###.###.###-##"),
            "cnpj" => Mask(Digits(Arg(0)), "##.###.###/####-##"),
            "cep" => Mask(Digits(Arg(0)), "#####-###"),
            "telefone" => FormatPhone(Digits(Arg(0))),
            "mask" => Mask(Digits(Arg(0)), ExpressionValues.Stringify(Arg(1))),
            "digits" => Digits(Arg(0)),
            "upper" => ExpressionValues.Stringify(Arg(0)).ToUpperInvariant(),
            "lower" => ExpressionValues.Stringify(Arg(0)).ToLowerInvariant(),
            "trim" => ExpressionValues.Stringify(Arg(0)).Trim(),
            "len" => (double)Len(Arg(0)),
            "join" => string.Join(ExpressionValues.Stringify(Arg(1)),
                ExpressionValues.AsEnumerable(Arg(0)).Select(ExpressionValues.Stringify)),
            "if" => ExpressionValues.Truthy(Arg(0)) ? Arg(1) : Arg(2),
            "coalesce" => args.FirstOrDefault(a => a is not null),
            _ when ctx.TryGetFormat(name, out var customMask) => Mask(Digits(Arg(0)), customMask),
            _ => throw new FormatException($"Unknown function '{name}'."),
        };
    }

    private static string FormatDate(object? value, string? format)
    {
        format = string.IsNullOrWhiteSpace(format) ? "dd/MM/yyyy" : format;
        return value switch
        {
            DateTime dt => dt.ToString(format, Br),
            DateTimeOffset dto => dto.ToString(format, Br),
            string s when DateTime.TryParse(s, Br, DateTimeStyles.None, out var parsed) => parsed.ToString(format, Br),
            string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed.ToString(format, Br),
            _ => ExpressionValues.Stringify(value),
        };
    }

    private static int Len(object? value) => value switch
    {
        null => 0,
        string s => s.Length,
        _ => ExpressionValues.AsEnumerable(value).Count(),
    };

    private static string Digits(object? value)
    {
        var text = ExpressionValues.Stringify(value);
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
            if (char.IsDigit(c)) sb.Append(c);
        return sb.ToString();
    }

    /// <summary>Fills the '#' placeholders of a pattern left-to-right with the given digits.</summary>
    private static string Mask(string digits, string pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return digits;

        var sb = new StringBuilder(pattern.Length);
        var d = 0;
        foreach (var c in pattern)
        {
            if (c == '#')
            {
                if (d < digits.Length) sb.Append(digits[d++]);
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static string FormatPhone(string digits) => digits.Length switch
    {
        11 => Mask(digits, "(##) #####-####"),
        10 => Mask(digits, "(##) ####-####"),
        _ => digits,
    };
}
