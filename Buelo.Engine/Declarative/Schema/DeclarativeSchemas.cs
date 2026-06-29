using System.Collections;
using System.Reflection;
using Buelo.Engine.Declarative.Modules;

namespace Buelo.Engine.Declarative.Schema;

/// <summary>
/// Generates a JSON Schema (draft-07) for each declarative <c>kind</c> by reflecting over its AST
/// type (blueprint §11). Feeding these to <c>monaco-yaml</c> gives autocomplete, inline validation
/// and hover in the editor — no language server. Recursion (e.g. nested blocks) uses <c>$ref</c>.
/// </summary>
public static class DeclarativeSchemas
{
    private static readonly Dictionary<string, Type> KindTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["report"] = typeof(ReportDefinition),
        ["component"] = typeof(ComponentModule),
        ["styles"] = typeof(StylesModule),
        ["formats"] = typeof(FormatsModule),
        ["lib"] = typeof(LibModule),
        ["validator"] = typeof(ValidatorModule),
        ["theme"] = typeof(ThemeModule),
    };

    public static IReadOnlyCollection<string> Kinds => KindTypes.Keys;

    public static bool TryGetType(string kind, out Type type) => KindTypes.TryGetValue(kind, out type!);

    public static object Generate(string kind)
        => TryGetType(kind, out var type)
            ? Generate(type)
            : throw new InvalidOperationException($"No schema for kind '{kind}'.");

    public static object Generate(Type root)
    {
        var definitions = new Dictionary<string, object?>();
        BuildObject(root, definitions);
        return new Dictionary<string, object?>
        {
            ["$schema"] = "http://json-schema.org/draft-07/schema#",
            ["$ref"] = $"#/definitions/{root.Name}",
            ["definitions"] = definitions,
        };
    }

    private static void BuildObject(Type type, Dictionary<string, object?> definitions)
    {
        if (definitions.ContainsKey(type.Name))
            return;

        var schema = new Dictionary<string, object?> { ["type"] = "object", ["additionalProperties"] = false };
        definitions[type.Name] = schema; // register early to break reference cycles

        var properties = new Dictionary<string, object?>();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            properties[CamelCase(property.Name)] = SchemaFor(property.PropertyType, definitions);

        schema["properties"] = properties;
    }

    private static object SchemaFor(Type type, Dictionary<string, object?> definitions)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(string))
            return new Dictionary<string, object?> { ["type"] = "string" };
        if (type == typeof(bool))
            return new Dictionary<string, object?> { ["type"] = "boolean" };
        if (type == typeof(int) || type == typeof(long) || type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return new Dictionary<string, object?> { ["type"] = "number" };
        if (type == typeof(object))
            return new Dictionary<string, object?>(); // any
        if (type.IsEnum)
            return new Dictionary<string, object?> { ["type"] = "string", ["enum"] = Enum.GetNames(type) };

        if (type.IsGenericType)
        {
            var generic = type.GetGenericTypeDefinition();
            var args = type.GetGenericArguments();

            if (generic == typeof(List<>) || generic == typeof(IReadOnlyList<>) || generic == typeof(IList<>))
                return new Dictionary<string, object?> { ["type"] = "array", ["items"] = SchemaFor(args[0], definitions) };

            if (generic == typeof(Dictionary<,>) || generic == typeof(IDictionary<,>))
                return new Dictionary<string, object?> { ["type"] = "object", ["additionalProperties"] = SchemaFor(args[1], definitions) };
        }

        if (typeof(IEnumerable).IsAssignableFrom(type))
            return new Dictionary<string, object?> { ["type"] = "array" };

        if (type.IsClass)
        {
            BuildObject(type, definitions);
            return new Dictionary<string, object?> { ["$ref"] = $"#/definitions/{type.Name}" };
        }

        return new Dictionary<string, object?>();
    }

    private static string CamelCase(string name)
        => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
