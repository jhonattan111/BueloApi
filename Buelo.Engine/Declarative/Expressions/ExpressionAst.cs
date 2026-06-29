namespace Buelo.Engine.Declarative.Expressions;

/// <summary>
/// AST for the <c>{{ }}</c> expression language (blueprint §6). Pure, single-line,
/// side-effect-free expressions. Control flow lives in directives, never here.
/// </summary>
internal abstract record Expr;

internal sealed record LiteralExpr(object? Value) : Expr;

internal sealed record IdentifierExpr(string Name) : Expr;

internal sealed record MemberExpr(Expr Target, string Name) : Expr;

internal sealed record IndexExpr(Expr Target, Expr Index) : Expr;

/// <summary>A function call by name: <c>currency(x)</c>, <c>sum(list, 'expr')</c>. Pipes desugar into this.</summary>
internal sealed record CallExpr(string Name, IReadOnlyList<Expr> Args) : Expr;

internal sealed record UnaryExpr(string Op, Expr Operand) : Expr;

internal sealed record BinaryExpr(string Op, Expr Left, Expr Right) : Expr;

internal sealed record TernaryExpr(Expr Condition, Expr WhenTrue, Expr WhenFalse) : Expr;
