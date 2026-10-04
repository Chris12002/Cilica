using System;
using System.Collections.Immutable;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Source storage and an optional nested field path, never a guessed native offset.</summary>
public sealed record ValueRootSlot(int Slot, ObjectRootKind Kind, int Index, int Block, ImmutableArray<ObjectFieldId> Fields);
public sealed record ValueMethodRoots(IrMethodId Method, ImmutableArray<ValueRootSlot> Slots,
    ImmutableArray<ObjectSafepoint> Safepoints, int ScalarSlots);

/// <summary>Phase A copies publish every embedded reference; this is a plan, not native registration.</summary>
public static class ValueRootAssignment
{
    public const int MaximumScalarSlots = 65536;

    public static ImmutableArray<ValueMethodRoots> Assign(ValueIrProgram program)
    {
        var rules = ValueIrValidator.ValidateAndPlan(program);
        var layouts = rules.Plans.ToDictionary(p => p.Type);
        var result = ImmutableArray.CreateBuilder<ValueMethodRoots>();
        int total = 0;
        foreach (var method in program.Methods)
        {
            var roots = ImmutableArray.CreateBuilder<ValueRootSlot>();
            var points = ImmutableArray.CreateBuilder<ObjectSafepoint>();
            int scalars = 0;
            void Root(ObjectRootKind kind, int index, int block, ImmutableArray<ObjectFieldId> path)
            {
                if (roots.Count == ObjectRootAssignment.MaximumRootsPerMethod)
                    throw new ArgumentException("Value method exceeds the 1024 phase A root-slot limit. " + method.Source.Name, nameof(program));
                roots.Add(new(roots.Count, kind, index, block, path));
            }
            void Add(ObjectRootKind kind, int index, IrType type, int block = -1)
            {
                int count = type.Kind == IrTypeKind.Value ? layouts[type.Class!].Slots.Length : 1;
                if (count > MaximumScalarSlots - total)
                    throw new ArgumentException("Value program exceeds the expanded scalar-slot limit.", nameof(program));
                total += count; scalars += count;
                if (type.Kind is IrTypeKind.Reference or IrTypeKind.NullReference) Root(kind, index, block, []);
                else if (type.Kind == IrTypeKind.Value)
                    foreach (var slot in layouts[type.Class!].Slots)
                        if (slot.Storage is ObjectStorage.Reference) Root(kind, index, block, slot.Fields);
            }
            for (int i = 0; i < method.Parameters.Length; i++) Add(ObjectRootKind.Parameter, i, method.Parameters[i]);
            for (int i = 0; i < method.Locals.Length; i++) Add(ObjectRootKind.Local, i, method.Locals[i]);
            foreach (var block in method.Blocks)
            {
                for (int i = 0; i < block.Inputs.Length; i++) Add(ObjectRootKind.BlockInput, i, block.Inputs[i], block.Id);
                for (int i = 0; i < block.Statements.Length; i++)
                    if (block.Statements[i] is IrStatement.Define define)
                    {
                        Add(ObjectRootKind.Value, define.Value.Id, define.Value.Type, block.Id);
                        if (define.Value.Expression is IrExpression.Call)
                            points.Add(new(i, define.Value.ILOffset, ObjectSafepointKind.Call, block.Id));
                    }
            }
            result.Add(new(IrMethodId.From(method.Source), roots.ToImmutable(), points.ToImmutable(), scalars));
        }
        return result.ToImmutable();
    }
}
