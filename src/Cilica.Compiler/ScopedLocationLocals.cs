using System;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Compiler;

/// <summary>Single-definition method-scoped borrows. Validate after ordinary edges and types.</summary>
internal static class ScopedLocationLocals
{
    private const int MaximumProofWork = 65536;
    internal static void Validate(IrMethod method, IReadOnlyList<List<int>> successors)
    {
        var locals = method.Locals.Select((t,i) => (Type:t,Index:i)).Where(t => t.Type.Kind == IrTypeKind.ManagedLocation).Select(t => t.Index).ToHashSet();
        if (locals.Count == 0) return;
        void Require(bool valid, string reason)
        { if (!valid) throw new ArgumentException("Invalid scoped ref local: " + reason + " " + method.Source.Name, nameof(method)); }
        Require(method.ExceptionRegions.IsEmpty, "exception-region lifetimes require later analysis.");
        var stores = new Dictionary<int,(int Block,int Statement)>();
        var reads = new Dictionary<int,List<(int Block,int Statement)>>();
        foreach (int local in locals) reads.Add(local, []);
        foreach (var block in method.Blocks)
            for (int i = 0; i < block.Statements.Length; ++i)
            {
                var statement = block.Statements[i];
                if (statement is IrStatement.StoreLocal store && locals.Contains(store.Local))
                    Require(stores.TryAdd(store.Local,(block.Id,i)), "a borrow local must have one assignment site.");
                if (statement is IrStatement.Define { Value.Expression: IrExpression.Local read } && locals.Contains(read.Index))
                    reads[read.Index].Add((block.Id,i));
            }
        var predecessors = Enumerable.Range(0, method.Blocks.Length).Select(_ => new List<int>()).ToArray();
        for (int source = 0; source < successors.Count; ++source)
            foreach (int target in successors[source]) predecessors[target].Add(source);
        int work = 0;
        foreach (int local in locals)
        {
            if (reads[local].Count == 0) continue;
            Require(stores.ContainsKey(local), "read before assignment.");
            var store = stores[local];
            var checkedBlocks = new HashSet<int>();
            foreach (var read in reads[local])
            {
                if (read.Block == store.Block) { Require(read.Statement > store.Statement, "read before assignment in its block."); continue; }
                if (!checkedBlocks.Add(read.Block)) continue;
                // The defining block must dominate this read: walk backwards,
                // cutting at that block, and reject any remaining path to entry.
                var pending = new Stack<int>(); var visited = new HashSet<int>(); pending.Push(read.Block);
                while (pending.TryPop(out int current))
                {
                    if (current == store.Block || !visited.Add(current)) continue;
                    Require(++work <= MaximumProofWork, "dominance proof work limit exceeded.");
                    Require(current != 0, "assignment does not dominate every read.");
                    foreach (int prior in predecessors[current])
                    {
                        Require(++work <= MaximumProofWork, "dominance proof work limit exceeded.");
                        pending.Push(prior);
                    }
                }
            }
        }
    }
}
