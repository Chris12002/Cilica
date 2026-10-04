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
        return ValidateComposition(program.Flow, program.Bindings, classes: false);
    }

    internal static ValueIrRules ValidateComposition(ManagedLocationIrProgram program,
        ImmutableArray<ObjectMethodBinding> bindings, bool classes)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(program.Flow);
        var flow = program.Flow;
        // Validate layouts before constructing rule lookup tables. Bindings are
        // checked on the value signatures before hidden scalar parameters exist.
        _ = ValueLayoutPlanner.Build(flow.Types);
        if (bindings.IsDefault || bindings.Any(b => b is null || b.Kind != ObjectMethodKind.Static
            && !(classes && b.Kind is ObjectMethodKind.Constructor or ObjectMethodKind.ObjectBaseConstructor)))
            throw new ArgumentException("Composed value methods require direct static bindings or explicitly supported class constructors.", nameof(program));
        var objects = new ObjectIrRules(new(new(flow.Entry, flow.Methods, flow.Types.References), bindings), locations: true, values: flow.Types);
        var rules = ValueIrValidator.ValidateAndPlan(flow, locations: true, objects);
        foreach (var method in flow.Methods)
            if (objects.Binding(IrMethodId.From(method.Source)).Kind != ObjectMethodKind.Static
                && method.Parameters.Concat(method.Locals).Concat(method.Blocks.SelectMany(b => b.Inputs))
                    .Concat(method.Blocks.SelectMany(b => b.Values).Select(v => v.Type))
                    .Any(p => p.Kind is IrTypeKind.Value or IrTypeKind.ManagedLocation))
                throw new ArgumentException("Class constructor value/borrow flow needs later lowering; constructor bodies require scalar/reference operations.", nameof(program));
        objects.DispatchMaps();
        var entry = flow.Methods.Single(m => IrMethodId.From(m.Source) == flow.Entry);
        if (objects.Binding(flow.Entry).Kind != ObjectMethodKind.Static || entry.ReturnType != IrType.Int32 || entry.Parameters.Any(p => p != IrType.Int32))
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
                    or IrExpression.NewArray or IrExpression.ArrayLength or IrExpression.LoadElement
                    || classes && d.Value.Expression is (IrExpression.NewObject or IrExpression.LoadField),
                IrStatement.StoreLocal or IrStatement.Invoke or IrStatement.StoreLocation
                    or IrStatement.InitializeLocation or IrStatement.StoreElement => true,
                IrStatement.StoreField when classes => true,
                _ => false,
            };
            if (!supported) throw new ArgumentException(classes
                ? "Value object IR does not yet compose string intrinsics, dispatch or other object operations."
                : "Value array IR does not yet compose class/string allocation or other object operations.", nameof(program));
        }
        return rules;
    }
}
