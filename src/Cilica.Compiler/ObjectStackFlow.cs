using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Cilica.Metadata;
using Cilica.Metadata.IL;

namespace Cilica.Compiler;

/// <summary>Monotone stack inference for Int32, null and direct-Object class references.</summary>
internal static class ObjectStackFlow
{
    internal static ImmutableArray<IrBlock> Infer(MethodInput input, ObjectTypeId objectBase,
        CilControlFlow.Partitioned partition,
        Func<int, int, int, ImmutableArray<IrType>, IrBlock> translate,
        Action<string, string, int> fail, Action<int, int> consume)
    {
        var commonObject = IrType.Reference(objectBase);
        int count = partition.Ranges.Count;
        var incoming = new ImmutableArray<IrType>[count]; incoming[0] = [];
        var blocks = new IrBlock[count]; var queue = new Queue<int>(); var queued = new bool[count];
        queue.Enqueue(0); queued[0] = true;
        int inputs = 0;
        while (queue.TryDequeue(out int id))
        {
            queued[id] = false;
            var range = partition.Ranges[id];
            var block = translate(id, range.Start, range.End, incoming[id]); blocks[id] = block;
            var values = block.Values;
            foreach (var edge in Edges(block.Terminator))
            {
                consume(edge.Arguments.Length + 1, block.Terminator.ILOffset);
                var candidate = edge.Arguments.Select(a => values[a].Type).ToImmutableArray();
                var prior = incoming[edge.Target]; bool changed = prior.IsDefault;
                if (prior.IsDefault)
                {
                    inputs += candidate.Length;
                    if (inputs > IrLimits.MaximumInputs) fail(DiagnosticIds.TranslationLimit, "Aggregate block-input limit exceeded.", block.Terminator.ILOffset);
                }
                else
                {
                    if (prior.Length != candidate.Length)
                        fail(DiagnosticIds.InconsistentStack, "Join receives incompatible evaluation-stack depths.", block.Terminator.ILOffset);
                    var merged = prior.ToBuilder();
                    for (int i = 0; i < prior.Length; i++)
                    {
                        var type = Join(prior[i], candidate[i]);
                        if (type is null) fail(DiagnosticIds.InconsistentStack, "Join cannot mix Int32 and reference stack values.", block.Terminator.ILOffset);
                        merged[i] = type!; changed |= type != prior[i];
                    }
                    candidate = merged.ToImmutable();
                }
                if (changed)
                {
                    incoming[edge.Target] = candidate;
                    if (!queued[edge.Target]) { queue.Enqueue(edge.Target); queued[edge.Target] = true; }
                }
            }
        }
        for (int i = 0; i < blocks.Length; i++)
            if (blocks[i] is null) fail(DiagnosticIds.ExecutionUnsupported, "Unreachable blocks remain outside the verified object profile.", input.Instructions[partition.Ranges[i].Start].Offset);
        return [.. blocks];

        IrType? Join(IrType left, IrType right)
        {
            if (left == right) return left;
            if (left == IrType.NullReference && right.Kind == IrTypeKind.Reference) return right;
            if (right == IrType.NullReference && left.Kind == IrTypeKind.Reference) return left;
            if (left.Kind == IrTypeKind.Reference && right.Kind == IrTypeKind.Reference) return commonObject;
            return null;
        }
    }

    private static IEnumerable<IrEdge> Edges(IrTerminator terminator) => terminator switch
    {
        IrJump jump => [jump.Edge], IrBranch branch => [branch.Taken, branch.NotTaken], _ => [],
    };
}
