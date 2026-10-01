using System.Collections.Immutable;

namespace Cilica.Compiler;

public enum ObjectRootKind { Parameter, Local, Value }
public sealed record ObjectRootSlot(int Slot, ObjectRootKind Kind, int Index);
public enum ObjectSafepointKind { Allocation, Call }
public sealed record ObjectSafepoint(int Statement, int ILOffset, ObjectSafepointKind Kind);
public sealed record ObjectMethodRoots(IrMethodId Method, ImmutableArray<ObjectRootSlot> Slots,
    ImmutableArray<ObjectSafepoint> Safepoints);

/// <summary>Phase A: assign distinct slots before emission; no liveness or slot reuse.</summary>
public static class ObjectRootAssignment
{
    public const int MaximumRootsPerMethod = 1024;

    public static ImmutableArray<ObjectMethodRoots> Assign(ObjectIrProgram program)
    {
        ObjectIrValidator.Validate(program);
        var result = ImmutableArray.CreateBuilder<ObjectMethodRoots>();
        foreach (var method in program.Flow.Methods)
        {
            if (method.Blocks.Length != 1 || !method.Blocks[0].Inputs.IsEmpty
                || method.Blocks[0].Terminator is not (IrReturn or IrVoidReturn))
                throw new System.ArgumentException("Native object emission requires straight-line methods; reference branches require slice 3b-c. " + method.Source.Name, nameof(program));
            var slots = ImmutableArray.CreateBuilder<ObjectRootSlot>();
            void Add(ObjectRootKind kind, int index, IrType type)
            {
                if (type.Kind is not (IrTypeKind.Reference or IrTypeKind.NullReference)) return;
                if (slots.Count == MaximumRootsPerMethod)
                    throw new System.ArgumentException("Native object method exceeds the 1024 phase A root-slot limit. " + method.Source.Name, nameof(program));
                slots.Add(new(slots.Count, kind, index));
            }
            for (int i = 0; i < method.Parameters.Length; i++) Add(ObjectRootKind.Parameter, i, method.Parameters[i]);
            for (int i = 0; i < method.Locals.Length; i++) Add(ObjectRootKind.Local, i, method.Locals[i]);
            var points = ImmutableArray.CreateBuilder<ObjectSafepoint>();
            for (int i = 0; i < method.Blocks[0].Statements.Length; i++)
            {
                var statement = method.Blocks[0].Statements[i];
                if (statement is IrStatement.Define define)
                {
                    Add(ObjectRootKind.Value, define.Value.Id, define.Value.Type);
                    if (define.Value.Expression is IrExpression.NewObject)
                        points.Add(new(i, define.Value.ILOffset, ObjectSafepointKind.Allocation));
                    else if (define.Value.Expression is IrExpression.Call)
                        points.Add(new(i, define.Value.ILOffset, ObjectSafepointKind.Call));
                }
                else if (statement is IrStatement.Invoke invoke)
                    points.Add(new(i, invoke.ILOffset, ObjectSafepointKind.Call));
            }
            result.Add(new(IrMethodId.From(method.Source), slots.ToImmutable(), points.ToImmutable()));
        }
        return result.ToImmutable();
    }
}
