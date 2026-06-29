using Buelo.Contracts;
using Buelo.Engine.Declarative.Modules;
using Buelo.Engine.Ir;
using Buelo.Engine.Renderers;

namespace Buelo.Engine.Declarative;

/// <summary>
/// Orchestrates the declarative pipeline: YAML (+ imported module YAMLs) → parse → interpret
/// (lowering) → <see cref="BueloDocument"/> → recipe → bytes. Can render inline YAML or a stored
/// report resolved by name from an <see cref="IDefinitionStore"/> (with its imports).
/// </summary>
public sealed class DeclarativeReportEngine(DeclarativeInterpreter interpreter, IValidatorExtensions? validatorExtensions = null)
{
    private readonly DeclarativeValidator _validator = new();
    private readonly IValidatorExtensions _validatorExtensions = validatorExtensions ?? new ValidatorExtensions();

    /// <summary>Parses and lowers a definition (plus optional module YAMLs) to the IR.</summary>
    public BueloDocument Build(string yaml, object? data, IEnumerable<string>? modules = null)
    {
        var definition = DeclarativeParser.Parse(yaml);
        var registry = modules is null ? ModuleRegistry.Empty : ModuleRegistry.Build(modules);
        ValidateData(definition, data, registry);
        return interpreter.Lower(definition, data, registry);
    }

    /// <summary>Renders a YAML definition + data (+ modules) straight to PDF bytes.</summary>
    public byte[] RenderPdf(string yaml, object? data, IEnumerable<string>? modules = null)
        => new BueloDocumentRenderer(Build(yaml, data, modules)).RenderPdf();

    /// <summary>Renders a YAML definition + data (+ modules) to XLSX bytes (same IR, ClosedXML recipe).</summary>
    public byte[] RenderExcel(string yaml, object? data, IEnumerable<string>? modules = null)
        => new BueloDocumentExcelRenderer().Render(Build(yaml, data, modules));

    /// <summary>Ejects an equivalent C# IDocument from the resolved IR (declarative → code, §10).</summary>
    public string EjectCSharp(string yaml, object? data, IEnumerable<string>? modules = null)
        => CSharpEjector.Eject(Build(yaml, data, modules));

    /// <summary>Loads a stored report and its imported modules from the definition store.</summary>
    public async Task<(ReportDefinition Definition, List<string> Modules)> LoadProjectAsync(
        string reportName, IDefinitionStore store, CancellationToken cancellationToken = default)
    {
        var reportYaml = await store.ReadAsync("report", reportName, cancellationToken)
            ?? throw new InvalidOperationException($"Report '{reportName}' was not found.");
        var definition = DeclarativeParser.Parse(reportYaml);

        var modules = new List<string>();
        foreach (var import in definition.Import)
        {
            foreach (var (kind, name) in import)
            {
                var moduleName = StripPin(name); // version pin is advisory in v1
                var moduleYaml = await store.ReadAsync(kind, moduleName, cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Imported {kind} '{moduleName}' (used by report '{reportName}') was not found.");
                modules.Add(moduleYaml);
            }
        }
        return (definition, modules);
    }

    /// <summary>Renders a stored report (resolved by name, with its imports) to PDF bytes.</summary>
    public async Task<byte[]> RenderStoredAsync(
        string reportName, object? data, IDefinitionStore store, CancellationToken cancellationToken = default)
    {
        var ir = await LowerStoredAsync(reportName, data, store, cancellationToken);
        return new BueloDocumentRenderer(ir).RenderPdf();
    }

    /// <summary>Renders a stored report to XLSX bytes (same IR, ClosedXML recipe).</summary>
    public async Task<byte[]> RenderStoredExcelAsync(
        string reportName, object? data, IDefinitionStore store, CancellationToken cancellationToken = default)
    {
        var ir = await LowerStoredAsync(reportName, data, store, cancellationToken);
        return new BueloDocumentExcelRenderer().Render(ir);
    }

    private async Task<Ir.BueloDocument> LowerStoredAsync(
        string reportName, object? data, IDefinitionStore store, CancellationToken cancellationToken)
    {
        var (definition, modules) = await LoadProjectAsync(reportName, store, cancellationToken);
        var registry = ModuleRegistry.Build(modules);
        ValidateData(definition, data, registry);
        return interpreter.Lower(definition, data, registry);
    }

    /// <summary>
    /// Pre-render validation gate (blueprint §8): evaluates each <c>validate:</c> expression and runs
    /// the named validator. Throws <see cref="InvalidOperationException"/> (→ 400) if any value is invalid.
    /// </summary>
    private void ValidateData(ReportDefinition definition, object? data, ModuleRegistry registry)
    {
        if (definition.Validate.Count == 0)
            return;

        var scope = new Dictionary<string, object?>
        {
            ["data"] = data is null ? null : TemplateEngine.ConvertToDynamic(data),
        };
        var context = registry.CreateExpressionContext();
        var errors = new List<string>();

        foreach (var (expression, validatorName) in definition.Validate)
        {
            if (!registry.TryGetValidator(validatorName, out var module))
                throw new InvalidOperationException($"Validator '{validatorName}' (used by validate: {expression}) was not found.");

            var value = ExpressionEvaluator.Evaluate(expression, scope, context);
            var result = _validator.Validate(module, value, context, _validatorExtensions);
            if (!result.Valid)
                errors.AddRange(result.Errors.Select(e => $"{expression} ({validatorName}): {e}"));
        }

        if (errors.Count > 0)
            throw new InvalidOperationException("Validation failed — " + string.Join("; ", errors));
    }

    private static string StripPin(string name)
    {
        var at = name.IndexOf('@');
        return at >= 0 ? name[..at] : name;
    }
}
