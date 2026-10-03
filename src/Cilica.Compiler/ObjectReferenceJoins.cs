using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Refines coarse stack types after the complete, validated catalog is available.</summary>
internal static class ObjectReferenceJoins
{
    internal static IrMethod Refine(IrMethod method, ObjectTypeId objectBase, ObjectTypeRelations relations,
        Action<int, int> consume, Action<string, int> fail)
    {
        // Only unequal non-null references lose information in the structural pass.
        // Exact/null-only joins need no second walk (including large forwarding loops).
        if (!method.Blocks.Any(b => b.Inputs.Contains(IrType.Reference(objectBase)))) return method;

        var incoming = method.Blocks.Select(b => new HashSet<ObjectTypeId>?[b.Inputs.Length]).ToArray();
        var queue = new Queue<int>(Enumerable.Range(0, method.Blocks.Length));
        var queued = Enumerable.Repeat(true, method.Blocks.Length).ToArray();
        while (queue.TryDequeue(out int id))
        {
            queued[id] = false;
            var block = method.Blocks[id];
            consume(block.Statements.Length + 1, block.ILOffset);
            var values = block.Values;
            foreach (var edge in Edges(block.Terminator))
            {
                consume(edge.Arguments.Length + 1, block.Terminator.ILOffset);
                bool changed = false;
                for (int slot = 0; slot < edge.Arguments.Length; slot++)
                {
                    var value = values[edge.Arguments[slot]];
                    // Stack inputs forward the whole set, never a prematurely chosen
                    // class/interface. All other producers have an explicit static type.
                    var candidate = value.Expression is IrExpression.StackInput input ? incoming[id][input.Index]
                        : value.Type.Kind == IrTypeKind.Reference ? relations.Ancestors[value.Type.Class!] : null;
                    if (candidate is null) continue; // Null supplies no reference constraint.
                    var prior = incoming[edge.Target][slot];
                    consume(candidate.Count + (prior?.Count ?? 0), block.Terminator.ILOffset);
                    if (prior is null) { incoming[edge.Target][slot] = new(candidate); changed = true; }
                    else
                    {
                        int before = prior.Count; prior.IntersectWith(candidate); changed |= before != prior.Count;
                    }
                }
                if (changed && !queued[edge.Target]) { queue.Enqueue(edge.Target); queued[edge.Target] = true; }
            }
        }

        var blocks = ImmutableArray.CreateBuilder<IrBlock>(method.Blocks.Length);
        foreach (var block in method.Blocks)
        {
            consume(block.Statements.Length + block.Inputs.Length + 1, block.ILOffset);
            var inputs = block.Inputs.ToBuilder();
            for (int slot = 0; slot < inputs.Count; slot++)
            {
                if (incoming[block.Id][slot] is not { } common) continue;
                // A unique minimum is assignable to every other common supertype.
                // Ambiguous interface intersections cannot be represented by one IR type.
                consume(common.Count, block.ILOffset);
                // Intersections of ancestor-closed sets are ancestor-closed.
                // A minimum therefore has exactly this set as its own ancestors;
                // the validated acyclic graph permits at most one such type.
                var minimal = common.Where(t => relations.Ancestors[t].Count == common.Count).ToArray();
                if (minimal.Length != 1)
                    fail("Reference stack join has no single most-specific common type. Store each branch into an explicitly typed local or cast before the join.", block.ILOffset);
                inputs[slot] = IrType.Reference(minimal.Single());
            }
            var refined = inputs.ToImmutable();
            blocks.Add(block with
            {
                Inputs = refined,
                Statements = [.. block.Statements.Select(s => s is IrStatement.Define { Value.Expression: IrExpression.StackInput input } definition
                    ? definition with { Value = definition.Value with { Type = refined[input.Index] } } : s)],
            });
        }
        return method with { Blocks = blocks.ToImmutable() };
    }

    private static IEnumerable<IrEdge> Edges(IrTerminator terminator) => terminator switch
    {
        IrJump jump => [jump.Edge], IrBranch branch => [branch.Taken, branch.NotTaken], _ => [],
    };
}
