namespace Buelo.Engine.Declarative.Modules;

/// <summary>
/// AST for the reusable module kinds (blueprint §8). Each is a <c>kind:</c> file imported by a
/// report: <c>styles</c> (named classes + extends), <c>formats</c> (masks), <c>lib</c> (named pure
/// expressions), <c>theme</c> (styles + page + palette), <c>component</c> (params + slots + body).
/// </summary>
public sealed class StylesModule
{
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, StyleDef> Classes { get; set; } = [];
}

public sealed class FormatsModule
{
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Formats { get; set; } = [];
}

public sealed class LibModule
{
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Expr { get; set; } = [];
}

public sealed class ThemeModule
{
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PageDef? Page { get; set; }
    public Dictionary<string, StyleDef> Styles { get; set; } = [];
    public Dictionary<string, string> Palette { get; set; } = [];
}

public sealed class ComponentModule
{
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, ParamDef> Params { get; set; } = [];
    public List<string> Slots { get; set; } = [];
    public List<ContentBlock> Body { get; set; } = [];
}

public sealed class ParamDef
{
    public string? Type { get; set; }
    public string? Default { get; set; }
}

/// <summary>
/// A validator (blueprint §8), with three escalating tiers: declarative (format/regex/checksum),
/// pure expression, or a reference to a registered C# extension.
/// </summary>
public sealed class ValidatorModule
{
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    // Tier 1 — declarative.
    public string? Format { get; set; }
    public List<ValidatorRule> Rules { get; set; } = [];

    // Tier 2 — pure expression. The value is bound to the first param name.
    public string? Expr { get; set; }
    public List<string> Params { get; set; } = [];

    // Tier 3 — reference to a registered C# extension (self-hosted).
    public string? Ref { get; set; }
}

public sealed class ValidatorRule
{
    public int? Digits { get; set; }
    public string? Regex { get; set; }
    public ChecksumRule? Checksum { get; set; }
}

public sealed class ChecksumRule
{
    public string Scheme { get; set; } = string.Empty;
    public List<int> Weights { get; set; } = [];
}
