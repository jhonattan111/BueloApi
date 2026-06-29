using Buelo.Engine.Declarative;

namespace Buelo.Tests.Engine;

public class ExpressionEngineTests
{
    private static readonly Dictionary<string, object?> Empty = new();

    private static object? Eval(string expr, IDictionary<string, object?> scope) =>
        ExpressionEvaluator.Evaluate(expr, scope);

    [Theory]
    [InlineData("1 + 2 * 3", 7d)]
    [InlineData("(1 + 2) * 3", 9d)]
    [InlineData("10 % 3", 1d)]
    [InlineData("2 > 1 && 3 >= 3", true)]
    [InlineData("1 > 2 || 5 == 5", true)]
    [InlineData("!false", true)]
    [InlineData("1 == 1 ? 10 : 20", 10d)]
    [InlineData("null ?? 42", 42d)]
    public void Evaluates_operators(string expr, object expected) =>
        Assert.Equal(expected, Eval(expr, Empty));

    [Fact]
    public void Resolves_nested_member_paths()
    {
        var scope = Dict(("data", Dict(("client", Dict(("name", "Contar"))))));
        Assert.Equal("Contar", Eval("data.client.name", scope));
    }

    [Fact]
    public void Indexing_into_list()
    {
        var scope = Dict(("data", Dict(("items", new List<object?> { "a", "b", "c" }))));
        Assert.Equal("b", Eval("data.items[1]", scope));
    }

    [Fact]
    public void Interpolates_index_plus_one()
    {
        var scope = Dict(("index", 0L));
        Assert.Equal("1", ExpressionEvaluator.Interpolate("{{ index + 1 }}", scope));
    }

    [Fact]
    public void Pipe_is_sugar_for_call()
    {
        var scope = Dict(("v", 1234.5));
        Assert.Equal(Eval("currency(v)", scope), Eval("v | currency", scope));
    }

    [Fact]
    public void Sum_aggregation_over_subexpression()
    {
        var items = new List<object?>
        {
            Dict(("price", 10.0), ("qty", 2.0)),
            Dict(("price", 5.0), ("qty", 3.0)),
        };
        var scope = Dict(("data", Dict(("items", items))));
        Assert.Equal(35d, Eval("sum(data.items, 'price * qty')", scope));
    }

    [Fact]
    public void Count_and_avg_aggregations()
    {
        var items = new List<object?> { Dict(("x", 2.0)), Dict(("x", 4.0)) };
        var scope = Dict(("data", Dict(("items", items))));
        Assert.Equal(2d, Eval("count(data.items)", scope));
        Assert.Equal(3d, Eval("avg(data.items, 'x')", scope));
    }

    [Fact]
    public void Currency_formats_as_brl()
    {
        var result = Eval("currency(1234.5)", Empty) as string;
        Assert.NotNull(result);
        Assert.StartsWith("R$", result);
        Assert.Contains("1.234", result);
        Assert.Contains("50", result);
    }

    [Fact]
    public void Cpf_masks_digits() =>
        Assert.Equal("123.456.789-09", Eval("cpf('12345678909')", Empty));

    [Fact]
    public void Unknown_function_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Eval("nonexistent(1)", Empty));
        Assert.Contains("nonexistent", ex.Message);
    }

    private static Dictionary<string, object?> Dict(params (string Key, object? Value)[] entries)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var (key, value) in entries)
            dict[key] = value;
        return dict;
    }
}
