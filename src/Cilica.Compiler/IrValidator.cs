using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Cilica.Compiler;

internal static class IrValidator
{
    internal static void Validate(IrMethod method)
    {
        void Require([DoesNotReturnIf(false)] bool condition, string message)
        {
            if (!condition) throw new ArgumentException("Invalid Int32 IR: " + message, nameof(method));
        }
        Require(method.Source is not null, "source is missing.");
        Require(method.Source.ContentHash is { Length: 64 } hash && hash.All(char.IsAsciiHexDigit), "source hash must be SHA-256 hexadecimal.");
        Require((uint)method.Source.Token >> 24 == 0x06 && (method.Source.Token & 0xffffff) != 0, "source needs a non-nil MethodDef token.");
        Require(!method.Parameters.IsDefault && method.Parameters.Length <= IrLimits.MaximumParameters && method.Parameters.All(p => p == IrType.Int32)
            && method.ReturnType == IrType.Int32, "signature is outside the bounded Int32 profile.");
        Require(!method.Locals.IsDefault && method.Locals.Length <= IrLimits.MaximumLocals && method.Locals.All(l => l == IrType.Int32), "locals must be bounded Int32 storage.");
        Require(!method.Blocks.IsDefault && method.Blocks.Length is > 0 and <= IrLimits.MaximumBlocks, "invalid block count.");
        int inputs = 0, statements = 0, edgeArguments = 0, previousBlockOffset = -1;
        for (int id = 0; id < method.Blocks.Length; id++)
        {
            IrBlock block = method.Blocks[id];
            Require(block is not null && block.Id == id && block.ILOffset > previousBlockOffset, "blocks need consecutive IDs and increasing IL offsets.");
            Require(!block.Inputs.IsDefault && block.Inputs.Length <= IrLimits.MaximumInputs && block.Inputs.All(t => t == IrType.Int32), "invalid block inputs.");
            inputs += block.Inputs.Length;
            Require(inputs <= IrLimits.MaximumInputs, "aggregate block-input limit exceeded.");
            previousBlockOffset = block.ILOffset;
        }
        Require(method.Blocks[0].ILOffset == 0 && method.Blocks[0].Inputs.IsEmpty, "entry block starts at zero with an empty stack.");
        var successors = new List<int>[method.Blocks.Length];
        foreach (IrBlock block in method.Blocks)
        {
            var targets = new List<int>(); successors[block.Id] = targets;
            Require(!block.Statements.IsDefault && block.Statements.Length <= IrLimits.MaximumStatements, "invalid statement array.");
            statements += block.Statements.Length;
            Require(statements <= IrLimits.MaximumStatements, "aggregate statement limit exceeded.");
            int values = 0, previousOffset = block.ILOffset;
            int end = block.Id + 1 < method.Blocks.Length ? method.Blocks[block.Id + 1].ILOffset : int.MaxValue;
            bool Earlier(int value) => value >= 0 && value < values;
            bool Local(int local) => local >= 0 && local < method.Locals.Length;
            void Offset(int offset)
            {
                Require(offset >= previousOffset && offset < end, "statement/terminator offset is outside its block or out of order.");
                previousOffset = offset;
            }
            foreach (IrStatement statement in block.Statements)
            {
                switch (statement)
                {
                    case IrStatement.Define define:
                        IrValue value = define.Value;
                        Require(value is not null && value.Id == values && value.Type == IrType.Int32, "values need consecutive IDs and Int32 types.");
                        Offset(value.ILOffset);
                        Require(value.Expression switch
                        {
                            IrExpression.Constant => true,
                            IrExpression.Parameter p => p.Index >= 0 && p.Index < method.Parameters.Length,
                            IrExpression.Local l => Local(l.Index),
                            IrExpression.StackInput s => s.Index >= 0 && s.Index < block.Inputs.Length,
                            IrExpression.Binary b => b.Operation is Int32Operation.AddWrapping or Int32Operation.SubtractWrapping or Int32Operation.MultiplyWrapping
                                && Earlier(b.Left) && Earlier(b.Right),
                            IrExpression.Compare c => BinaryComparison(c.Comparison) && Earlier(c.Left) && Earlier(c.Right),
                            _ => false,
                        }, "expression, parameter or operand is invalid.");
                        values++;
                        break;
                    case IrStatement.StoreLocal store:
                        Offset(store.ILOffset);
                        Require(Local(store.Local) && Earlier(store.Value), "invalid local store."); break;
                    default: Require(false, "missing or unknown statement."); break;
                }
            }
            Require(block.Terminator is not null, "missing block terminator."); Offset(block.Terminator.ILOffset);
            switch (block.Terminator)
            {
                case IrReturn returned: Require(Earlier(returned.Value), "invalid returned value."); break;
                case IrJump jump: Edge(jump.Edge); break;
                case IrBranch branch:
                    Require(Earlier(branch.Left) && (branch.Comparison is Int32Comparison.Zero or Int32Comparison.Nonzero
                        ? branch.Right is null : BinaryComparison(branch.Comparison) && branch.Right is int right && Earlier(right)), "invalid branch condition.");
                    Edge(branch.Taken); Edge(branch.NotTaken); break;
                default: Require(false, "unknown block terminator."); break;
            }
            void Edge(IrEdge edge)
            {
                Require(edge is not null && edge.Target >= 0 && edge.Target < method.Blocks.Length, "invalid edge target.");
                Require(!edge.Arguments.IsDefault && edge.Arguments.Length == method.Blocks[edge.Target].Inputs.Length
                    && edge.Arguments.All(Earlier), "edge arguments must match the target's typed inputs.");
                edgeArguments += edge.Arguments.Length;
                Require(edgeArguments <= IrLimits.MaximumEdgeArguments, "aggregate edge-argument limit exceeded.");
                targets.Add(edge.Target);
            }
        }
        var visited = new HashSet<int>(); var pending = new Stack<int>(); pending.Push(0);
        while (pending.TryPop(out int id))
            if (visited.Add(id)) foreach (int target in successors[id]) pending.Push(target);
        Require(visited.Count == method.Blocks.Length, "all blocks must be reachable from entry.");
    }

    internal static bool BinaryComparison(Int32Comparison comparison) => (uint)comparison < (uint)Int32Comparison.Zero;
}
