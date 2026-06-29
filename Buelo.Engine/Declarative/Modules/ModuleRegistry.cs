using Buelo.Engine.Declarative.Expressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Buelo.Engine.Declarative.Modules;

/// <summary>
/// Holds the modules a report imports, indexed for resolution (blueprint §8). Style classes are
/// flattened with <c>extends</c> applied; collisions across modules are an error. Theme classes are
/// merged at lower precedence than explicit <c>styles</c> classes.
/// </summary>
public sealed class ModuleRegistry
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private readonly Dictionary<string, StyleDef> _classes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _formats = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _libs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ComponentModule> _components = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ValidatorModule> _validators = new(StringComparer.OrdinalIgnoreCase);

    public PageDef? ThemePage { get; private set; }

    public static readonly ModuleRegistry Empty = new();

    public bool TryGetClass(string? name, out StyleDef style)
    {
        style = default!;
        return !string.IsNullOrWhiteSpace(name) && _classes.TryGetValue(name, out style!);
    }

    public bool TryGetComponent(string name, out ComponentModule component)
        => _components.TryGetValue(name, out component!);

    public bool TryGetValidator(string name, out ValidatorModule validator)
        => _validators.TryGetValue(name, out validator!);

    public ExpressionContext CreateExpressionContext() => new(_libs, _formats);

    /// <summary>Parses and indexes a set of module YAML documents. Throws on kind/name/class collisions.</summary>
    public static ModuleRegistry Build(IEnumerable<string> moduleYamls)
    {
        var registry = new ModuleRegistry();
        var rawStyleClasses = new Dictionary<string, StyleDef>(StringComparer.OrdinalIgnoreCase);
        var themeClasses = new Dictionary<string, StyleDef>(StringComparer.OrdinalIgnoreCase);

        foreach (var yaml in moduleYamls)
        {
            if (string.IsNullOrWhiteSpace(yaml))
                continue;

            var kind = Probe(yaml);
            switch (kind)
            {
                case "styles":
                    var styles = Deserializer.Deserialize<StylesModule>(yaml);
                    foreach (var (className, def) in styles.Classes)
                    {
                        if (rawStyleClasses.ContainsKey(className))
                            throw new InvalidOperationException($"Style class '{className}' is defined more than once.");
                        rawStyleClasses[className] = def;
                    }
                    break;

                case "formats":
                    var formats = Deserializer.Deserialize<FormatsModule>(yaml);
                    foreach (var (name, mask) in formats.Formats)
                    {
                        if (registry._formats.ContainsKey(name))
                            throw new InvalidOperationException($"Format '{name}' is defined more than once.");
                        registry._formats[name] = mask;
                    }
                    break;

                case "lib":
                    var lib = Deserializer.Deserialize<LibModule>(yaml);
                    if (registry._libs.ContainsKey(lib.Name))
                        throw new InvalidOperationException($"Lib '{lib.Name}' is defined more than once.");
                    registry._libs[lib.Name] = lib.Expr.ToDictionary(e => e.Key, e => StripBraces(e.Value));
                    break;

                case "component":
                    var component = Deserializer.Deserialize<ComponentModule>(yaml);
                    if (registry._components.ContainsKey(component.Name))
                        throw new InvalidOperationException($"Component '{component.Name}' is defined more than once.");
                    registry._components[component.Name] = component;
                    break;

                case "theme":
                    var theme = Deserializer.Deserialize<ThemeModule>(yaml);
                    registry.ThemePage ??= theme.Page;
                    foreach (var (className, def) in theme.Styles)
                        themeClasses[className] = def; // theme classes are defaults (lower precedence)
                    break;

                case "validator":
                    var validator = Deserializer.Deserialize<ValidatorModule>(yaml);
                    if (registry._validators.ContainsKey(validator.Name))
                        throw new InvalidOperationException($"Validator '{validator.Name}' is defined more than once.");
                    registry._validators[validator.Name] = validator;
                    break;

                default:
                    throw new InvalidOperationException($"Unknown or unsupported module kind '{kind}'.");
            }
        }

        // Theme classes are the base; explicit styles classes override them.
        foreach (var (className, def) in themeClasses)
            registry._classes[className] = def;
        foreach (var (className, def) in rawStyleClasses)
            registry._classes[className] = def;

        registry.ResolveExtends();
        return registry;
    }

    /// <summary>Applies the <c>extends</c> chain so each class is fully merged (parent first, child overrides).</summary>
    private void ResolveExtends()
    {
        var resolved = new Dictionary<string, StyleDef>(StringComparer.OrdinalIgnoreCase);

        StyleDef Resolve(string name, HashSet<string> visiting)
        {
            if (resolved.TryGetValue(name, out var done))
                return done;
            if (!_classes.TryGetValue(name, out var def))
                return new StyleDef();
            if (!visiting.Add(name))
                throw new InvalidOperationException($"Circular 'extends' detected at style class '{name}'.");

            var merged = def;
            if (!string.IsNullOrWhiteSpace(def.Extends))
                merged = MergeStyleDefs(Resolve(def.Extends!, visiting), def);

            visiting.Remove(name);
            resolved[name] = merged;
            return merged;
        }

        foreach (var name in _classes.Keys.ToList())
            Resolve(name, []);

        foreach (var (name, def) in resolved)
            _classes[name] = def;
    }

    /// <summary>Override wins per-property over base.</summary>
    public static StyleDef MergeStyleDefs(StyleDef baseStyle, StyleDef over) => new()
    {
        Color = over.Color ?? baseStyle.Color,
        Background = over.Background ?? baseStyle.Background,
        Bold = over.Bold ?? baseStyle.Bold,
        Italic = over.Italic ?? baseStyle.Italic,
        Size = over.Size ?? baseStyle.Size,
        Align = over.Align ?? baseStyle.Align,
        Padding = over.Padding ?? baseStyle.Padding,
        Border = over.Border ?? baseStyle.Border,
        PaddingY = over.PaddingY ?? baseStyle.PaddingY,
        BorderBottom = over.BorderBottom ?? baseStyle.BorderBottom,
    };

    private sealed class KindProbe
    {
        public string Kind { get; set; } = string.Empty;
    }

    private static string Probe(string yaml)
    {
        var kind = Deserializer.Deserialize<KindProbe>(yaml)?.Kind;
        if (string.IsNullOrWhiteSpace(kind))
            throw new InvalidOperationException("Module is missing a 'kind'.");
        return kind.Trim().ToLowerInvariant();
    }

    /// <summary>Lib expressions may be written wrapped in <c>{{ }}</c>; store the bare expression.</summary>
    private static string StripBraces(string value)
    {
        var trimmed = value.Trim();
        return trimmed.StartsWith("{{") && trimmed.EndsWith("}}")
            ? trimmed[2..^2].Trim()
            : trimmed;
    }
}
