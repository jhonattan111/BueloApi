using Buelo.Contracts;
using Buelo.Engine.Declarative;
using Buelo.Engine.Declarative.Modules;

namespace Buelo.Tests.Engine;

public class DeclarativeValidatorTests
{
    private static readonly DeclarativeValidator Validator = new();

    private static DataValidationResult Validate(string yaml, string name, object? value, IValidatorExtensions? ext = null)
    {
        var registry = ModuleRegistry.Build([yaml]);
        Assert.True(registry.TryGetValidator(name, out var module));
        return Validator.Validate(module, value, registry.CreateExpressionContext(), ext ?? new ValidatorExtensions());
    }

    private const string CpfValidator = """
        kind: validator
        name: cpf
        format: "###.###.###-##"
        rules:
          - { digits: 11 }
          - { checksum: { scheme: mod11, weights: [10, 9, 8, 7, 6, 5, 4, 3, 2] } }
        """;

    [Fact]
    public void Tier1_valid_cpf_passes()
    {
        var result = Validate(CpfValidator, "cpf", "529.982.247-25");
        Assert.True(result.Valid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Tier1_bad_checksum_fails()
    {
        var result = Validate(CpfValidator, "cpf", "529.982.247-00");
        Assert.False(result.Valid);
    }

    [Fact]
    public void Tier1_wrong_digit_count_fails()
    {
        var result = Validate(CpfValidator, "cpf", "123");
        Assert.False(result.Valid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Tier2_expression_validator()
    {
        const string yaml = """
            kind: validator
            name: len11
            expr: "len(digits(id)) == 11"
            params: [id]
            """;

        Assert.True(Validate(yaml, "len11", "123.456.789-01").Valid);
        Assert.False(Validate(yaml, "len11", "123").Valid);
    }

    [Fact]
    public void Tier3_extension_reference()
    {
        const string yaml = """
            kind: validator
            name: ext
            ref: My.Check
            """;
        var extensions = new ValidatorExtensions().Register("My.Check", v => v?.ToString() == "ok");

        Assert.True(Validate(yaml, "ext", "ok", extensions).Valid);
        Assert.False(Validate(yaml, "ext", "no", extensions).Valid);
    }

    [Fact]
    public void Tier3_unregistered_reference_fails()
    {
        const string yaml = "kind: validator\nname: ext\nref: Missing.Method";

        var result = Validate(yaml, "ext", "anything");
        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Contains("not registered"));
    }
}
