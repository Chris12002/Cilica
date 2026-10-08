using System;
using System.Collections.Generic;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Proves receiver non-nullness at evaluation time, without moving null checks or calls.</summary>
internal static class NonNullFieldReturnProof
{
    internal const string Unsupported = "Direct struct-return calls stored into heap fields require a destination proven non-null before RHS evaluation; materialized copies and explicit managed locations are supported.";
    private const int MaximumWork = 1_048_576;
    private const int CheckedReceiver = int.MaxValue;

    internal static void Validate(IrMethod method, bool constructorReceiver, bool stableReceiver,
        HashSet<(int Block, int Receiver, int Offset)> stores, Action<int, string, bool> fail)
    {
        // A local whose address exists may be modified by a callee. Excluding it
        // throughout the method also covers aliases that survive block boundaries.
        var addressed = method.Blocks.SelectMany(b => b.Values).Select(v => v.Expression)
            .OfType<IrExpression.AddressLocal>().Select(a => a.Index).ToHashSet();
        var incoming = new HashSet<int>?[method.Blocks.Length];
        var pending = new Queue<int>(); var queued = new bool[method.Blocks.Length];
        int work = 0, inputBase = method.Locals.Length;
        Merge(0, []);
        foreach (var region in method.ExceptionRegions)
        {
            Merge(region.HandlerStart, []);
            if (region.FilterStart is int filter) Merge(filter, []);
        }
        while (pending.TryDequeue(out int id))
        {
            queued[id] = false;
            var (locals, values) = Evaluate(id);
            foreach (var edge in Edges(method.Blocks[id].Terminator))
            {
                // A leave can execute finally handlers that change local slots.
                var next = method.Blocks[id].Terminator is IrLeave ? new HashSet<int>() : new HashSet<int>(locals);
                for (int i = 0; i < edge.Arguments.Length; i++)
                {
                    Consume(method.Blocks[id].Terminator.ILOffset);
                    if (values.Contains(edge.Arguments[i])) next.Add(inputBase + i);
                }
                Merge(edge.Target, next);
            }
        }
        // Check only the fixed point. Early optimistic facts are retracted when
        // another predecessor or loop back-edge supplies a nullable reference.
        foreach (var group in stores.GroupBy(s => s.Block))
        {
            var (_, values) = Evaluate(group.Key);
            foreach (var store in group)
                if (!values.Contains(store.Receiver)) fail(store.Offset, Unsupported, false);
        }

        (HashSet<int> Locals, HashSet<int> Values) Evaluate(int id)
        {
            var entry = incoming[id] ?? [];
            var locals = entry.Where(i => i < inputBase).ToHashSet();
            var values = new HashSet<int>();
            var definitions = method.Blocks[id].Values;
            bool checkedReceiver = constructorReceiver || stableReceiver && entry.Contains(CheckedReceiver);
            foreach (var statement in method.Blocks[id].Statements)
            {
                Consume(method.Blocks[id].ILOffset);
                if (statement is IrStatement.Define { Value: var value })
                {
                    bool known = value.Expression switch
                    {
                        IrExpression.NewObject => true,
                        IrExpression.Parameter { Index: 0 } => checkedReceiver,
                        IrExpression.Local local => !addressed.Contains(local.Index) && locals.Contains(local.Index),
                        IrExpression.StackInput input => entry.Contains(inputBase + input.Index),
                        IrExpression.CastClass cast => values.Contains(cast.Operand),
                        _ => false,
                    };
                    if (known) values.Add(value.Id);
                    int checkedValue = value.Expression switch
                    {
                        IrExpression.LoadField field => field.Receiver,
                        IrExpression.AddressField field => field.Receiver,
                        _ => -1,
                    };
                    if (DirectReceiver(checkedValue)) checkedReceiver = true;
                }
                else if (statement is IrStatement.StoreLocal store)
                {
                    if (!addressed.Contains(store.Local) && values.Contains(store.Value)) locals.Add(store.Local);
                    else locals.Remove(store.Local);
                }
                else if (statement is IrStatement.StoreField field && DirectReceiver(field.Receiver))
                    checkedReceiver = true;
            }
            if (checkedReceiver) locals.Add(CheckedReceiver);
            return (locals, values);

            // A completed field access establishes non-nullness only for later
            // reads of the immutable instance argument. Joins intersect this fact.
            // Never retroactively mark the earlier receiver SSA value as safe.
            bool DirectReceiver(int value) => stableReceiver && value >= 0
                && definitions[value].Expression is IrExpression.Parameter { Index: 0 };
        }
        void Merge(int id, HashSet<int> next)
        {
            Consume(method.Blocks[id].ILOffset, next.Count + 1);
            bool changed;
            if (incoming[id] is not { } prior) { incoming[id] = next; changed = true; }
            else
            {
                Consume(method.Blocks[id].ILOffset, prior.Count);
                int count = prior.Count; prior.IntersectWith(next); changed = prior.Count != count;
            }
            if (changed && !queued[id]) { queued[id] = true; pending.Enqueue(id); }
        }
        void Consume(int offset, int count = 1)
        {
            work += count;
            if (work > MaximumWork) fail(offset, "Non-null field-return proof work limit exceeded.", true);
        }
    }
    private static IEnumerable<IrEdge> Edges(IrTerminator terminator) => terminator switch
    {
        IrJump jump => [jump.Edge], IrBranch branch => [branch.Taken, branch.NotTaken], IrLeave leave => [leave.Edge], _ => [],
    };
}
