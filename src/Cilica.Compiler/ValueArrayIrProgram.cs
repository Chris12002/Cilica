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
        ImmutableArray<ObjectMethodBinding> bindings, bool classes, bool boxing = false, bool delegates = false, ManagedExceptionRules? exceptions = null,
        ObjectInterfaceDispatchPlan? interfaces = null, ImmutableArray<ObjectVirtualDispatch> dispatch = default)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(program.Flow);
        var flow = program.Flow;
        // Validate layouts before constructing rule lookup tables. Bindings are
        // checked on the value signatures before hidden scalar parameters exist.
        _ = ValueLayoutPlanner.Build(flow.Types);
        if (bindings.IsDefault || bindings.Any(b => b is null || b.Kind != ObjectMethodKind.Static
            && !(flow.Types.References.Integer64 && b.Kind is ObjectMethodKind.ValueConstructor or ObjectMethodKind.ValueInstance or ObjectMethodKind.PrimitiveInstance)
            && !((delegates || classes && flow.Types.References.Integer64) && b.Kind == ObjectMethodKind.Instance)
            && !(classes && b.Kind is ObjectMethodKind.Constructor or ObjectMethodKind.ObjectBaseConstructor)))
            throw new ArgumentException("Composed value methods require direct static bindings or explicitly supported class constructors.", nameof(program));
        if (interfaces is not null && !flow.Types.References.Integer64)
            throw new ArgumentException("Composed interface dispatch requires the explicit integer-object catalog.", nameof(program));
        if (!dispatch.IsDefaultOrEmpty && !flow.Types.References.Integer64)
            throw new ArgumentException("Composed class dispatch requires the explicit integer-object catalog.", nameof(program));
        var objects = new ObjectIrRules(new(new(flow.Entry, flow.Methods, flow.Types.References), bindings) { InterfaceDispatch = interfaces, Dispatch = dispatch.IsDefault ? [] : dispatch }, locations: true, values: flow.Types, delegates: delegates);
        var rules = ValueIrValidator.ValidateAndPlan(flow, locations: true, objects, boxing, exceptions);
        foreach (var method in flow.Methods)
            if (objects.Binding(IrMethodId.From(method.Source)).Kind is not (ObjectMethodKind.Static or ObjectMethodKind.ValueConstructor or ObjectMethodKind.ValueInstance or ObjectMethodKind.PrimitiveInstance)
                && !(flow.Types.References.Integer64 && objects.Binding(IrMethodId.From(method.Source)).Kind is ObjectMethodKind.Instance or ObjectMethodKind.Constructor)
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
                    || flow.Types.References.Integer64 && d.Value.Expression is (IrExpression.Constant64 or IrExpression.Binary64
                        or IrExpression.Not64 or IrExpression.ConvertInteger or IrExpression.IntegerCast)
                    || classes && d.Value.Expression is (IrExpression.NewObject or IrExpression.LoadField)
                    || boxing && d.Value.Expression is (IrExpression.BoxValue or IrExpression.UnboxValue or IrExpression.UnboxLocation)
                    || exceptions is not null && d.Value.Expression is (IrExpression.NewException or IrExpression.ExceptionMessage or IrExpression.ExceptionInnerException or IrExpression.ExceptionTypeName or IrExpression.ExceptionParameterName or IrExpression.IsInstance or IrExpression.CastClass or IrExpression.StringLiteral or IrExpression.StringLength or IrExpression.StringEquals)
                    || exceptions is not null && flow.Types.References.Integer64 && d.Value.Expression is (IrExpression.LoadStatic or IrExpression.EmptyArray or IrExpression.StringCodeUnit or IrExpression.StringFromCodeUnits or IrExpression.StringConcat or IrExpression.InvariantCulture or IrExpression.IntegerTextFind or IrExpression.IntegerTextPublish)
                    || delegates && d.Value.Expression is (IrExpression.NewDelegate or IrExpression.CallDelegate),
                IrStatement.StoreLocal or IrStatement.Invoke or IrStatement.StoreLocation
                    or IrStatement.InitializeLocation or IrStatement.StoreElement => true,
                IrStatement.StoreField when classes => true,
                IrStatement.StoreStatic when classes && exceptions is not null && flow.Types.References.Integer64 => true,
                IrStatement.InvokeDelegate when delegates => true,
                _ => false,
            };
            if (!supported) throw new ArgumentException(classes
                ? "Value object IR does not yet compose string intrinsics, dispatch or other object operations."
                : "Value array IR does not yet compose class/string allocation or other object operations.", nameof(program));
        }
        return rules;
    }
}
