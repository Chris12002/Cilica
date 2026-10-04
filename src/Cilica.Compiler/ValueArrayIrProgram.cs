using System;
using System.Collections.Immutable;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Inline copies and synchronous borrows composed with bounded vector allocation/access.</summary>
public sealed record ValueArrayIrProgram(ManagedLocationIrProgram Flow, ImmutableArray<ObjectMethodBinding> Bindings)
{
    public const string Profile = "value-arrays-ir-v1";
}

public static class ValueArrayIrValidator
{
    public static void Validate(ValueArrayIrProgram program) => _ = ValidateAndPlan(program);

    internal static ValueIrRules ValidateAndPlan(ValueArrayIrProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(program.Flow);
        ArgumentNullException.ThrowIfNull(program.Flow.Flow);
        var flow = program.Flow.Flow;
        // Validate layouts before constructing rule lookup tables. Bindings are
        // checked on the value signatures before hidden scalar parameters exist.
        _ = ValueLayoutPlanner.Build(flow.Types);
        if (program.Bindings.IsDefault || program.Bindings.Any(b => b is null || b.Kind != ObjectMethodKind.Static))
            throw new ArgumentException("Value array methods require direct static bindings.", nameof(program));
        var objects = new ObjectIrRules(new(new(flow.Entry, flow.Methods, flow.Types.References), program.Bindings), locations: true, values: flow.Types);
        var rules = ValueIrValidator.ValidateAndPlan(flow, locations: true, objects);
        objects.DispatchMaps();
        var entry = flow.Methods.Single(m => IrMethodId.From(m.Source) == flow.Entry);
        if (entry.ReturnType != IrType.Int32 || entry.Parameters.Any(p => p != IrType.Int32))
            throw new ArgumentException("Value array entry requires an Int32 boundary.", nameof(program));
        foreach (var statement in flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Statements))
        {
            bool supported = statement switch
            {
                IrStatement.Define d => d.Value.Expression is IrExpression.Constant or IrExpression.Null
                    or IrExpression.Parameter or IrExpression.Local or IrExpression.StackInput
                    or IrExpression.Binary or IrExpression.Unary or IrExpression.Compare or IrExpression.Call
                    or IrExpression.DefaultValue or IrExpression.ValueField or IrExpression.WithValueField
                    or IrExpression.AddressLocal or IrExpression.AddressParameter or IrExpression.AddressField
                    or IrExpression.LocationField or IrExpression.ReadOnlyLocation or IrExpression.LoadLocation
                    or IrExpression.NewArray or IrExpression.ArrayLength or IrExpression.LoadElement,
                IrStatement.StoreLocal or IrStatement.Invoke or IrStatement.StoreLocation
                    or IrStatement.InitializeLocation or IrStatement.StoreElement => true,
                _ => false,
            };
            if (!supported) throw new ArgumentException("Value array IR does not yet compose class/string allocation or other object operations.", nameof(program));
        }
        return rules;
    }
}
