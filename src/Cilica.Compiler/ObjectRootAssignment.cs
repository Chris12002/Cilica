using System.Collections.Immutable;

namespace Cilica.Compiler;

public enum ObjectRootKind { Parameter, Local, Value, BlockInput }
public sealed record ObjectRootSlot(int Slot, ObjectRootKind Kind, int Index, int Block = -1, bool LocationOwner = false);
public enum ObjectSafepointKind { Allocation, Call }
public sealed record ObjectSafepoint(int Statement, int ILOffset, ObjectSafepointKind Kind, int Block = 0);
public sealed record ObjectMethodRoots(IrMethodId Method, ImmutableArray<ObjectRootSlot> Slots,
    ImmutableArray<ObjectSafepoint> Safepoints);

/// <summary>Phase A: assign distinct slots before emission; no liveness or slot reuse.</summary>
public static class ObjectRootAssignment
{
    public const int MaximumRootsPerMethod = 1024;

    public static ImmutableArray<ObjectMethodRoots> Assign(ObjectIrProgram program)
    {
        ObjectIrValidator.Validate(program);
        return AssignValidated(program);
    }

    public static ImmutableArray<ObjectMethodRoots> AssignLocations(ScalarLocationIrProgram program)
    {
        ScalarLocationIrValidator.Validate(program);
        return AssignValidated(program.Flow, locations: true);
    }

    public static ImmutableArray<ObjectMethodRoots> AssignBoxes(ScalarBoxedIrProgram program)
    {
        ScalarBoxedIrValidator.Validate(program);
        return AssignValidated(program.Flow.Flow, locations: true);
    }

    public static ImmutableArray<ObjectMethodRoots> AssignDelegates(ScalarDelegateIrProgram program)
    {
        ScalarDelegateIrValidator.Validate(program);
        return AssignValidated(program.Flow.Flow.Flow, locations: true);
    }

    private static ImmutableArray<ObjectMethodRoots> AssignValidated(ObjectIrProgram program, bool locations = false)
    {
        var result = ImmutableArray.CreateBuilder<ObjectMethodRoots>();
        int expandedSlots = 0;
        foreach (var method in program.Flow.Methods)
        {
            var slots = ImmutableArray.CreateBuilder<ObjectRootSlot>();
            void Add(ObjectRootKind kind, int index, IrType type, int block = -1)
            {
                if (locations)
                {
                    expandedSlots += type.Kind == IrTypeKind.ManagedLocation ? 2 : 1;
                    if (expandedSlots > ValueRootAssignment.MaximumScalarSlots)
                        throw new System.ArgumentException("Scalar location program exceeds the expanded storage limit.", nameof(program));
                }
                if (type.Kind is not (IrTypeKind.Reference or IrTypeKind.NullReference or IrTypeKind.ManagedLocation)) return;
                if (slots.Count == MaximumRootsPerMethod)
                    throw new System.ArgumentException("Native object method exceeds the 1024 phase A root-slot limit. " + method.Source.Name, nameof(program));
                slots.Add(new(slots.Count, kind, index, block, type.Kind == IrTypeKind.ManagedLocation));
            }
            for (int i = 0; i < method.Parameters.Length; i++) Add(ObjectRootKind.Parameter, i, method.Parameters[i]);
            for (int i = 0; i < method.Locals.Length; i++) Add(ObjectRootKind.Local, i, method.Locals[i]);
            var points = ImmutableArray.CreateBuilder<ObjectSafepoint>();
            foreach (var block in method.Blocks)
            {
                for (int i = 0; i < block.Inputs.Length; i++) Add(ObjectRootKind.BlockInput, i, block.Inputs[i], block.Id);
                for (int i = 0; i < block.Statements.Length; i++)
                {
                    var statement = block.Statements[i];
                    if (statement is IrStatement.Define define)
                    {
                        Add(ObjectRootKind.Value, define.Value.Id, define.Value.Type, block.Id);
                        if (define.Value.Expression is IrExpression.NewObject or IrExpression.NewArray or IrExpression.StringLiteral or IrExpression.StringConcat or IrExpression.BoxScalars or IrExpression.NewDelegate)
                            points.Add(new(i, define.Value.ILOffset, ObjectSafepointKind.Allocation, block.Id));
                        else if (define.Value.Expression is IrExpression.Call or IrExpression.CallDelegate)
                            points.Add(new(i, define.Value.ILOffset, ObjectSafepointKind.Call, block.Id));
                    }
                    else if (statement is IrStatement.InvokeDelegate delegateCall)
                        points.Add(new(i, delegateCall.ILOffset, ObjectSafepointKind.Call, block.Id));
                    else if (statement is IrStatement.Invoke invoke)
                        points.Add(new(i, invoke.ILOffset, ObjectSafepointKind.Call, block.Id));
                }
            }
            result.Add(new(IrMethodId.From(method.Source), slots.ToImmutable(), points.ToImmutable()));
        }
        return result.ToImmutable();
    }
}
