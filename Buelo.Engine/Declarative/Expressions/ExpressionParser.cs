namespace Buelo.Engine.Declarative.Expressions;

/// <summary>
/// Recursive-descent parser for the expression grammar (lowest → highest precedence):
/// pipe · ternary · ?? · || · &amp;&amp; · == != · &lt; &lt;= &gt; &gt;= · + - · * / % · unary · postfix(. [] call) · primary.
/// </summary>
internal sealed class ExpressionParser
{
    private readonly List<Token> _tokens;
    private int _pos;

    private ExpressionParser(List<Token> tokens) => _tokens = tokens;

    public static Expr Parse(string expression)
    {
        var parser = new ExpressionParser(ExpressionLexer.Tokenize(expression));
        var expr = parser.ParseExpression();
        if (parser.Current.Type != TokenType.End)
            throw new FormatException($"Unexpected token '{parser.Current.Text}' in expression: {expression}");
        return expr;
    }

    private Token Current => _tokens[_pos];

    private bool MatchOp(string op)
    {
        if (Current.Type == TokenType.Operator && Current.Text == op)
        {
            _pos++;
            return true;
        }
        return false;
    }

    private void ExpectOp(string op)
    {
        if (!MatchOp(op))
            throw new FormatException($"Expected '{op}' but found '{Current.Text}'.");
    }

    private string ExpectIdentifier()
    {
        if (Current.Type != TokenType.Identifier)
            throw new FormatException($"Expected an identifier but found '{Current.Text}'.");
        var name = Current.Text;
        _pos++;
        return name;
    }

    private Expr ParseExpression() => ParsePipe();

    // x | f | g(a)  ≡  g(f(x), a)
    private Expr ParsePipe()
    {
        var left = ParseTernary();
        while (MatchOp("|"))
        {
            var name = ExpectIdentifier();
            var args = new List<Expr> { left };
            if (MatchOp("("))
            {
                args.AddRange(ParseArguments());
                ExpectOp(")");
            }
            left = new CallExpr(name, args);
        }
        return left;
    }

    private Expr ParseTernary()
    {
        var condition = ParseCoalesce();
        if (MatchOp("?"))
        {
            var whenTrue = ParseExpression();
            ExpectOp(":");
            var whenFalse = ParseExpression();
            return new TernaryExpr(condition, whenTrue, whenFalse);
        }
        return condition;
    }

    private Expr ParseCoalesce() => ParseBinary(ParseOr, "??");
    private Expr ParseOr() => ParseBinary(ParseAnd, "||");
    private Expr ParseAnd() => ParseBinary(ParseEquality, "&&");
    private Expr ParseEquality() => ParseBinary(ParseComparison, "==", "!=");
    private Expr ParseComparison() => ParseBinary(ParseAdditive, "<", "<=", ">", ">=");
    private Expr ParseAdditive() => ParseBinary(ParseMultiplicative, "+", "-");
    private Expr ParseMultiplicative() => ParseBinary(ParseUnary, "*", "/", "%");

    private Expr ParseBinary(Func<Expr> next, params string[] operators)
    {
        var left = next();
        while (Current.Type == TokenType.Operator && Array.IndexOf(operators, Current.Text) >= 0)
        {
            var op = Current.Text;
            _pos++;
            var right = next();
            left = new BinaryExpr(op, left, right);
        }
        return left;
    }

    private Expr ParseUnary()
    {
        if (MatchOp("!")) return new UnaryExpr("!", ParseUnary());
        if (MatchOp("-")) return new UnaryExpr("-", ParseUnary());
        return ParsePostfix();
    }

    private Expr ParsePostfix()
    {
        var expr = ParsePrimary();
        while (true)
        {
            if (MatchOp("."))
            {
                expr = new MemberExpr(expr, ExpectIdentifier());
            }
            else if (MatchOp("["))
            {
                var index = ParseExpression();
                ExpectOp("]");
                expr = new IndexExpr(expr, index);
            }
            else
            {
                return expr;
            }
        }
    }

    private Expr ParsePrimary()
    {
        var token = Current;

        switch (token.Type)
        {
            case TokenType.Number:
                _pos++;
                return new LiteralExpr(token.Value);

            case TokenType.String:
                _pos++;
                return new LiteralExpr(token.Value);

            case TokenType.Identifier:
                _pos++;
                if (MatchOp("(")) // function call
                {
                    var args = ParseArguments();
                    ExpectOp(")");
                    return new CallExpr(token.Text, args);
                }
                return new IdentifierExpr(token.Text);

            case TokenType.Operator when token.Text == "(":
                _pos++;
                var inner = ParseExpression();
                ExpectOp(")");
                return inner;

            default:
                throw new FormatException($"Unexpected token '{token.Text}' in expression.");
        }
    }

    private List<Expr> ParseArguments()
    {
        var args = new List<Expr>();
        if (Current.Type == TokenType.Operator && Current.Text == ")")
            return args; // empty arg list

        do
        {
            args.Add(ParseExpression());
        } while (MatchOp(","));

        return args;
    }
}
