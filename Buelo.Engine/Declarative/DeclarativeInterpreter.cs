using Buelo.Engine.Declarative.Modules;
using Buelo.Engine.Ir;

namespace Buelo.Engine.Declarative;

/// <summary>
/// Lowers a <see cref="ReportDefinition"/> AST plus data (and optional imported modules) into a
/// fully-resolved <see cref="BueloDocument"/> IR. Stateless and thread-safe: each call spins up a
/// per-render <see cref="DeclarativeLowering"/> worker.
/// </summary>
public sealed class DeclarativeInterpreter
{
    public BueloDocument Lower(ReportDefinition definition, object? data)
        => Lower(definition, data, ModuleRegistry.Empty);

    public BueloDocument Lower(ReportDefinition definition, object? data, ModuleRegistry modules)
        => new DeclarativeLowering(modules).Lower(definition, data);
}
