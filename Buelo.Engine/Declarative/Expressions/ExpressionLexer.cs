using System.Globalization;

namespace Buelo.Engine.Declarative.Expressions;

internal enum TokenType { Number, String, Identifier, Operator, End }

internal readonly record struct Token(TokenType Type, string Text, object? Value);

/// <summary>
/// Tokenizes an expression string. Multi-char operators are matched greedily so
/// <c>||</c>, <c>&amp;&amp;</c>, <c>==</c>, <c>!=</c>, <c>&lt;=</c>, <c>&gt;=</c> and <c>??</c> win over their prefixes.
/// </summary>
internal static class ExpressionLexer
{
    private static readonly string[] MultiCharOperators = ["??", "||", "&&", "==", "!=", "<=", ">="];

    public static List<Token> Tokenize(string input)
    {
        var tokens = new List<Token>();
        var i = 0;

        while (i < input.Length)
        {
            var c = input[i];

            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (char.IsDigit(c) || (c == '.' && i + 1 < input.Length && char.IsDigit(input[i + 1])))
            {
                var start = i;
                while (i < input.Length && (char.IsDigit(input[i]) || input[i] == '.')) i++;
                var text = input[start..i];
                tokens.Add(new Token(TokenType.Number, text, double.Parse(text, CultureInfo.InvariantCulture)));
                continue;
            }

            if (c is '"' or '\'')
            {
                var quote = c;
                i++;
                var start = i;
                while (i < input.Length && input[i] != quote) i++;
                if (i >= input.Length)
                    throw new FormatException($"Unterminated string literal in expression: {input}");
                var text = input[start..i];
                i++; // closing quote
                tokens.Add(new Token(TokenType.String, text, text));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < input.Length && (char.IsLetterOrDigit(input[i]) || input[i] == '_')) i++;
                var text = input[start..i];
                tokens.Add(text switch
                {
                    "true" => new Token(TokenType.Number, text, true),
                    "false" => new Token(TokenType.Number, text, false),
                    "null" => new Token(TokenType.Number, text, null),
                    _ => new Token(TokenType.Identifier, text, null),
                });
                continue;
            }

            var two = i + 1 < input.Length ? input.Substring(i, 2) : null;
            if (two is not null && Array.IndexOf(MultiCharOperators, two) >= 0)
            {
                tokens.Add(new Token(TokenType.Operator, two, null));
                i += 2;
                continue;
            }

            if ("+-*/%!<>?:()[],.|".IndexOf(c) >= 0)
            {
                tokens.Add(new Token(TokenType.Operator, c.ToString(), null));
                i++;
                continue;
            }

            throw new FormatException($"Unexpected character '{c}' in expression: {input}");
        }

        tokens.Add(new Token(TokenType.End, string.Empty, null));
        return tokens;
    }
}
