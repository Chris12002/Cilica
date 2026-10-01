using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Cilica.Compiler;

internal static class IrValidator
{
    internal static IrMethod Validate(IrProgram program, ObjectTypeCatalog? catalog = null, ObjectIrRules? objectRules = null)
    {
        void Require([DoesNotReturnIf(false)] bool condition, string message)
        {
            if (!condition) throw new ArgumentException((catalog is null ? "Invalid Int32 program IR: " : "Invalid reference program IR: ") + message, nameof(program));
        }
        Require(program.Entry is not null && !program.Methods.IsDefault && program.Methods.Length is > 0 and <= ProgramLimits.MaximumMethods,
            "entry and bounded methods are required.");
        var methods = new Dictionary<IrMethodId, IrMethod>();
        foreach (IrMethod method in program.Methods)
        {
            Require(method is not null && method.Source is not null, "method source is missing.");
            // Validate hash shape before deriving a native name/key.
            Require(method.Source.ContentHash is { Length: 64 } hash && hash.All(char.IsAsciiHexDigit), "invalid source hash.");
            Require(methods.TryAdd(IrMethodId.From(method.Source), method), "duplicate method identity/native symbol.");
        }
        Require(methods.TryGetValue(program.Entry, out IrMethod? entry), "entry is outside the program.");
        var resources = new ProgramResources();
        foreach (IrMethod method in program.Methods)
        {
            Validate(method, methods, catalog, objectRules);
            Require(resources.Add(method), "aggregate program IR limit exceeded.");
        }
        var visited = new HashSet<IrMethodId>(); var pending = new Stack<IrMethodId>(); pending.Push(program.Entry);
        while (pending.TryPop(out IrMethodId? key))
            if (visited.Add(key))
                foreach (IrStatement statement in methods[key].Blocks.SelectMany(b => b.Statements))
                    switch (statement)
                    {
                        case IrStatement.Define { Value.Expression: IrExpression.Call call }: pending.Push(call.Target); break;
                        case IrStatement.Define { Value.Expression: IrExpression.NewObject allocated }: pending.Push(allocated.Constructor); break;
                        case IrStatement.Invoke call: pending.Push(call.Target); break;
                    }
        Require(visited.Count == methods.Count, "program contains methods unreachable from entry.");
        return entry;
    }

    internal static void Validate(IrMethod method, IReadOnlyDictionary<IrMethodId, IrMethod>? methods = null, ObjectTypeCatalog? catalog = null, ObjectIrRules? objectRules = null)
    {
        int locationOffset = 0;
        void Require([DoesNotReturnIf(false)] bool condition, string message)
        {
            if (condition) return;
            string origin = objectRules is null ? "" : " " + method.Source?.Name + " token 0x" +
                method.Source?.Token.ToString("x8", System.Globalization.CultureInfo.InvariantCulture) + " IL_" + locationOffset.ToString("x4", System.Globalization.CultureInfo.InvariantCulture);
            if (!condition) throw new ArgumentException((catalog is null ? "Invalid Int32 IR: " : "Invalid reference IR: ") + message + origin, nameof(method));
        }
        var types = new IrTypeRules(catalog);
        Require(method.Source is not null, "source is missing.");
        Require(method.Source.AssemblyIdentity is not null, "assembly identity is missing.");
        Require(method.Source.ContentHash is { Length: 64 } hash && hash.All(char.IsAsciiHexDigit), "source hash must be SHA-256 hexadecimal.");
        Require((uint)method.Source.Token >> 24 == 0x06 && (method.Source.Token & 0xffffff) != 0, "source needs a non-nil MethodDef token.");
        Require(!method.Parameters.IsDefault && method.Parameters.Length <= IrLimits.MaximumParameters && method.Parameters.All(p => types.Valid(p, storage: true))
            && (types.Valid(method.ReturnType, storage: true) || objectRules is not null && method.ReturnType == IrType.Void), "signature is outside the bounded value profile.");
        Require(!method.Locals.IsDefault && method.Locals.Length <= IrLimits.MaximumLocals && method.Locals.All(l => types.Valid(l, storage: true)), "locals must be bounded typed storage.");
        Require(!method.Blocks.IsDefault && method.Blocks.Length is > 0 and <= IrLimits.MaximumBlocks, "invalid block count.");
        int inputs = 0, statements = 0, edgeArguments = 0, callArguments = 0, previousBlockOffset = -1;
        for (int id = 0; id < method.Blocks.Length; id++)
        {
            IrBlock block = method.Blocks[id];
            Require(block is not null && block.Id == id && block.ILOffset > previousBlockOffset, "blocks need consecutive IDs and increasing IL offsets.");
            Require(!block.Inputs.IsDefault && block.Inputs.Length <= IrLimits.MaximumInputs && block.Inputs.All(t => types.Valid(t, storage: false)), "invalid block inputs.");
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
            var valueTypes = new List<IrType>();
            var expressions = new List<IrExpression>();
            int values = 0, previousOffset = block.ILOffset;
            int end = block.Id + 1 < method.Blocks.Length ? method.Blocks[block.Id + 1].ILOffset : int.MaxValue;
            bool Earlier(int value) => value >= 0 && value < values;
            bool Local(int local) => local >= 0 && local < method.Locals.Length;
            void Offset(int offset)
            {
                Require(offset >= previousOffset && offset < end, "statement/terminator offset is outside its block or out of order.");
                previousOffset = offset; locationOffset = offset;
            }
            foreach (IrStatement statement in block.Statements)
            {
                switch (statement)
                {
                    case IrStatement.Define define:
                        IrValue value = define.Value;
                        Require(value is not null && value.Id == values && types.Valid(value.Type, storage: false), "values need consecutive IDs and valid types.");
                        Offset(value.ILOffset);
                        Require(value.Expression switch
                        {
                            IrExpression.Constant => value.Type == IrType.Int32,
                            IrExpression.Null => catalog is not null && value.Type == IrType.NullReference,
                            IrExpression.Parameter p => p.Index >= 0 && p.Index < method.Parameters.Length && value.Type == method.Parameters[p.Index],
                            IrExpression.Local l => Local(l.Index) && value.Type == method.Locals[l.Index],
                            IrExpression.StackInput s => s.Index >= 0 && s.Index < block.Inputs.Length && value.Type == block.Inputs[s.Index],
                            IrExpression.Binary b => value.Type == IrType.Int32 && (b.Operation is Int32Operation.AddWrapping or Int32Operation.SubtractWrapping or Int32Operation.MultiplyWrapping
                                or Int32Operation.BitwiseAnd or Int32Operation.BitwiseOr or Int32Operation.BitwiseXor
                                or Int32Operation.ShiftLeft or Int32Operation.ShiftRightSigned or Int32Operation.ShiftRightUnsigned
                                or Int32Operation.DivideSigned or Int32Operation.DivideUnsigned
                                or Int32Operation.RemainderSigned or Int32Operation.RemainderUnsigned
                                or Int32Operation.AddCheckedSigned or Int32Operation.SubtractCheckedSigned or Int32Operation.MultiplyCheckedSigned
                                or Int32Operation.AddCheckedUnsigned or Int32Operation.SubtractCheckedUnsigned or Int32Operation.MultiplyCheckedUnsigned)
                                && Earlier(b.Left) && Earlier(b.Right) && valueTypes[b.Left] == IrType.Int32 && valueTypes[b.Right] == IrType.Int32,
                            IrExpression.Unary u => value.Type == IrType.Int32 && u.Operation == Int32UnaryOperation.BitwiseNot && Earlier(u.Operand) && valueTypes[u.Operand] == IrType.Int32,
                            IrExpression.Compare c => value.Type == IrType.Int32 && BinaryComparison(c.Comparison) && Earlier(c.Left) && Earlier(c.Right)
                                && types.Comparable(c.Comparison, valueTypes[c.Left], valueTypes[c.Right]),
                            IrExpression.Call c => c.Target is not null && methods is not null && methods.TryGetValue(c.Target, out IrMethod? target)
                                && !c.Arguments.IsDefault && c.Arguments.Length <= IrLimits.MaximumParameters
                                && !target.Parameters.IsDefault && c.Arguments.Length == target.Parameters.Length && c.Arguments.All(Earlier)
                                && value.Type == target.ReturnType && c.Arguments.Select((argument, index) => types.Assignable(valueTypes[argument], target.Parameters[index])).All(ok => ok)
                                && (objectRules is null ? !c.NullCheckReceiver : objectRules.Call(method, c.Target, c.Arguments, c.NullCheckReceiver, valueTypes)),
                            IrExpression.NewObject n => objectRules is not null && value.Type == objectRules.AllocationType(n.Constructor)
                                && objectRules.Call(method, n.Constructor, n.Arguments, false, valueTypes, allocation: true),
                            IrExpression.LoadField f => objectRules is not null && objectRules.Field(method, f.Field, f.Receiver, valueTypes, out var storage)
                                && value.Type == storage,
                            _ => false,
                        }, "expression, parameter or operand is invalid.");
                        bool throwing = value.Expression is IrExpression.Call or IrExpression.NewObject or IrExpression.LoadField ||
                            value.Expression is IrExpression.Binary binary && Int32Operations.MayFault(binary.Operation);
                        Require(define.Exceptional == (throwing ? IrExceptionTarget.MethodExit : null),
                            "throwing definitions need an explicit method-exit edge; pure definitions cannot have one.");
                        if (value.Expression is IrExpression.Call call)
                        {
                            callArguments += call.Arguments.Length;
                            Require(callArguments <= ProgramLimits.MaximumCallArguments, "call-argument limit exceeded.");
                        }
                        if (value.Expression is IrExpression.NewObject allocated)
                        {
                            callArguments += allocated.Arguments.Length;
                            Require(callArguments <= ProgramLimits.MaximumCallArguments, "call-argument limit exceeded.");
                        }
                        valueTypes.Add(value.Type); expressions.Add(value.Expression); values++;
                        break;
                    case IrStatement.StoreLocal store:
                        Offset(store.ILOffset);
                        Require(Local(store.Local) && Earlier(store.Value) && types.Assignable(valueTypes[store.Value], method.Locals[store.Local]), "invalid local store."); break;
                    case IrStatement.Invoke invoke:
                        Offset(invoke.ILOffset);
                        Require(objectRules is not null && invoke.Exceptional == IrExceptionTarget.MethodExit
                            && invoke.Target is not null && methods is not null && methods.TryGetValue(invoke.Target, out var callee)
                            && callee.ReturnType == IrType.Void && !invoke.Arguments.IsDefault && invoke.Arguments.Length <= IrLimits.MaximumParameters
                            && (objectRules.Call(method, invoke.Target, invoke.Arguments, invoke.NullCheckReceiver, valueTypes)
                                || objectRules.BaseCall(method, invoke, valueTypes, expressions)), "invalid void call or base initialization.");
                        callArguments += invoke.Arguments.Length;
                        Require(callArguments <= ProgramLimits.MaximumCallArguments, "call-argument limit exceeded."); break;
                    case IrStatement.StoreField store:
                        Offset(store.ILOffset);
                        Require(objectRules is not null && objectRules.Store(method, store, valueTypes, expressions), "invalid field store, access or readonly receiver."); break;
                    default: Require(false, "missing or unknown statement."); break;
                }
            }
            Require(block.Terminator is not null, "missing block terminator."); Offset(block.Terminator.ILOffset);
            switch (block.Terminator)
            {
                case IrReturn returned: Require(Earlier(returned.Value) && types.Assignable(valueTypes[returned.Value], method.ReturnType), "invalid returned value."); break;
                case IrVoidReturn: Require(objectRules is not null && method.ReturnType == IrType.Void, "invalid void return."); break;
                case IrJump jump: Edge(jump.Edge); break;
                case IrBranch branch:
                    Require(Earlier(branch.Left) && (branch.Comparison is Int32Comparison.Zero or Int32Comparison.Nonzero
                        ? branch.Right is null : BinaryComparison(branch.Comparison) && branch.Right is int right && Earlier(right) && types.Comparable(branch.Comparison, valueTypes[branch.Left], valueTypes[right])), "invalid branch condition.");
                    Edge(branch.Taken); Edge(branch.NotTaken); break;
                default: Require(false, "unknown block terminator."); break;
            }
            void Edge(IrEdge edge)
            {
                Require(edge is not null && edge.Target >= 0 && edge.Target < method.Blocks.Length, "invalid edge target.");
                Require(!edge.Arguments.IsDefault && edge.Arguments.Length == method.Blocks[edge.Target].Inputs.Length
                    && edge.Arguments.All(Earlier) && edge.Arguments.Select((argument, index) => types.Assignable(valueTypes[argument], method.Blocks[edge.Target].Inputs[index])).All(ok => ok), "edge arguments must match the target's typed inputs.");
                edgeArguments += edge.Arguments.Length;
                Require(edgeArguments <= IrLimits.MaximumEdgeArguments, "aggregate edge-argument limit exceeded.");
                targets.Add(edge.Target);
            }
        }
        var visited = new HashSet<int>(); var pending = new Stack<int>(); pending.Push(0);
        while (pending.TryPop(out int id))
            if (visited.Add(id)) foreach (int target in successors[id]) pending.Push(target);
        Require(visited.Count == method.Blocks.Length, "all blocks must be reachable from entry.");
        objectRules?.Method(method);
    }

    internal static bool BinaryComparison(Int32Comparison comparison) => (uint)comparison < (uint)Int32Comparison.Zero;
}
