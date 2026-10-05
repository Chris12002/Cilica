using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Scalar boxing operations with exact logical payload plans; not a public struct ABI.</summary>
public sealed record ScalarBoxedIrProgram(ScalarLocationIrProgram Flow, ValueTypeCatalog Values)
{
    public const string Profile = "scalar-boxed-ir-v1";
}

public static class ScalarBoxedIrValidator
{
    public static void Validate(ScalarBoxedIrProgram program) => _ = ValidateAndPlan(program);
    internal static ScalarBoxingRules ValidateAndPlan(ScalarBoxedIrProgram program, bool delegates = false, ManagedExceptionRules? exceptions = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(program.Flow);
        ArgumentNullException.ThrowIfNull(program.Flow.Flow);
        ArgumentNullException.ThrowIfNull(program.Flow.Flow.Flow);
        var rules = new ScalarBoxingRules(program.Values);
        if (program.Values.References != program.Flow.Flow.Flow.Types)
            throw new ArgumentException("Scalar boxes must share the exact reference catalog.", nameof(program));
        ObjectIrValidator.Validate(program.Flow.Flow, locations: true, rules, delegates: delegates, exceptions: exceptions);
        return rules;
    }
}

internal sealed class ScalarBoxingRules
{
    internal ImmutableArray<ValueTypePlan> Plans { get; }
    private readonly Dictionary<ObjectTypeId, ValueTypePlan> _plans;
    private readonly IrTypeRules _types;
    private readonly ObjectTypeId _object;
    internal ScalarBoxingRules(ValueTypeCatalog values)
    {
        Plans = ValueLayoutPlanner.Build(values);
        _plans = Plans.ToDictionary(p => p.Type);
        _object = values.References.ObjectBase;
        _types = new(values.References, locations: true);
    }
    private static IrType Scalar(ObjectStorage storage) => storage switch
    {
        ObjectStorage.Int32 => IrType.Int32, ObjectStorage.Int64 => IrType.Int64,
        ObjectStorage.Reference reference => IrType.Reference(reference.Type),
        _ => throw new ArgumentException("Box payload leaf is not scalar."),
    };
    internal bool Box(IrExpression.BoxScalars box, IrType result, IReadOnlyList<IrType> values) => box.Type is not null
        && _plans.TryGetValue(box.Type, out var plan) && result == IrType.Reference(_object)
        && !box.Values.IsDefault && box.Values.Length == plan.Slots.Length && box.Values.Length <= IrLimits.MaximumParameters
        && box.Values.Select((id, index) => id >= 0 && id < values.Count && _types.Assignable(values[id], Scalar(plan.Slots[index].Storage))).All(ok => ok);
    internal bool Check(ObjectTypeId? type, int operand, IrType result, IReadOnlyList<IrType> values) => type is not null
        && _plans.ContainsKey(type) && result == IrType.Reference(_object) && Reference(operand, values);
    internal bool Slot(IrExpression.AddressBoxSlot slot, IrType result, IReadOnlyList<IrType> values) => slot.Type is not null
        && _plans.TryGetValue(slot.Type, out var plan) && slot.Slot >= 0 && slot.Slot < plan.Slots.Length
        && Reference(slot.Receiver, values) && ManagedLocationRules.Address(Scalar(plan.Slots[slot.Slot].Storage), result);
    private static bool Reference(int id, IReadOnlyList<IrType> values) => id >= 0 && id < values.Count
        && values[id].Kind is IrTypeKind.Reference or IrTypeKind.NullReference;
}
