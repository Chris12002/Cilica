using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Eliminates inline copies into existing scalar/reference IR; no boxing or managed allocation.</summary>
public static class ValueScalarLowering
{
    public const string Profile = "value-scalars-v1";
    public const string LocationProfile = "value-location-scalars-v1";

    public static ReferenceIrProgram Lower(ValueIrProgram program)
    {
        // Bound expanded storage before creating lowered temporaries and packets.
        _ = ValueRootAssignment.Assign(program);
        var result = LowerCore(program);
        ReferenceIrValidator.Validate(result);
        return result;
    }

    public static ScalarLocationIrProgram LowerLocations(ManagedLocationIrProgram program, ImmutableArray<ObjectMethodBinding> bindings)
    {
        _ = ValueRootAssignment.Assign(program);
        if (bindings.IsDefault || bindings.Any(b => b is null || b.Kind != ObjectMethodKind.Static))
            throw new ArgumentException("Value location lowering requires direct static method bindings.", nameof(bindings));
        var result = new ScalarLocationIrProgram(new(LowerCore(program.Flow), bindings));
        _ = ObjectRootAssignment.AssignLocations(result);
        return result;
    }

    private static ReferenceIrProgram LowerCore(ValueIrProgram program)
    {
        var plans = ValueLayoutPlanner.Build(program.Types).ToDictionary(p => p.Type);
        // A borrowed struct expands into one carrier/owner pair per leaf. Bound
        // this before constructing any expanded arrays, including field aliases.
        int slots = 0;
        foreach (var type in program.Methods.SelectMany(m => m.Parameters.Concat(m.Locals)
            .Concat(m.Blocks.SelectMany(b => b.Inputs)).Concat(m.Blocks.SelectMany(b => b.Values).Select(v => v.Type))))
        {
            var element = type.Kind == IrTypeKind.ManagedLocation ? type.Element! : type;
            int count = (element.Kind == IrTypeKind.Value ? plans[element.Class!].Slots.Length : 1)
                * (type.Kind == IrTypeKind.ManagedLocation ? 2 : 1);
            Require(count <= ValueRootAssignment.MaximumScalarSlots - slots, "expanded location storage limit exceeded.");
            slots += count;
        }
        var ranges = new Dictionary<ObjectFieldId, (int Start, int Count)>();
        foreach (var type in program.Types.Types)
        {
            int start = 0;
            foreach (var field in type.Fields)
            {
                int count = field.Storage is ObjectStorage.Value value ? plans[value.Type].Slots.Length : 1;
                ranges.Add(field.Id, (start, count)); start += count;
            }
        }
        static IrType Scalar(ObjectStorage storage) => storage switch
        {
            ObjectStorage.Int32 => IrType.Int32,
            ObjectStorage.Reference reference => IrType.Reference(reference.Type),
            _ => throw new InvalidOperationException("Validated layout has a non-scalar leaf."),
        };
        ImmutableArray<IrType> Expand(IrType type) => type.Kind switch
        {
            IrTypeKind.Value => [.. plans[type.Class!].Slots.Select(slot => Scalar(slot.Storage))],
            IrTypeKind.ManagedLocation when type.Element!.Kind == IrTypeKind.Value =>
                [.. plans[type.Element.Class!].Slots.Select(slot => IrType.Location(Scalar(slot.Storage), type.ReadOnly))],
            _ => [type],
        };
        static void Require(bool condition, string message)
        {
            if (!condition) throw new ArgumentException("Value scalar lowering: " + message, nameof(program));
        }
        var entry = program.Methods.Single(m => IrMethodId.From(m.Source) == program.Entry);
        Require(entry.ReturnType == IrType.Int32 && entry.Parameters.All(p => p == IrType.Int32),
            "the entry must retain the existing packed Int32 boundary.");
        Require(program.Methods.All(m => m.ReturnType.Kind != IrTypeKind.Value),
            "struct return packets require later lowering; only scalar-returning methods are currently supported.");
        var lowered = ImmutableArray.CreateBuilder<IrMethod>();
        int statements = 0, arguments = 0, edges = 0;
        foreach (var method in program.Methods)
        {
            var parameters = ImmutableArray.CreateBuilder<IrType>(); var locals = ImmutableArray.CreateBuilder<IrType>();
            var parameterSlots = new int[method.Parameters.Length]; var localSlots = new int[method.Locals.Length];
            for (int i = 0; i < parameterSlots.Length; i++)
            {
                parameterSlots[i] = parameters.Count; var expanded = Expand(method.Parameters[i]);
                Require(expanded.Length <= IrLimits.MaximumParameters - parameters.Count, "expanded parameter limit exceeded.");
                parameters.AddRange(expanded);
            }
            for (int i = 0; i < localSlots.Length; i++)
            {
                localSlots[i] = locals.Count; var expanded = Expand(method.Locals[i]);
                Require(expanded.Length <= IrLimits.MaximumLocals - locals.Count, "expanded local limit exceeded.");
                locals.AddRange(expanded);
            }
            // Typed zero-initialized locals preserve the declared reference type of
            // default fields, without casting null or allocating a wrapper object.
            var zeroReferences = new Dictionary<IrType, int>();
            int ZeroReference(IrType type)
            {
                if (zeroReferences.TryGetValue(type, out int index)) return index;
                Require(locals.Count < IrLimits.MaximumLocals, "default reference local limit exceeded.");
                index = locals.Count; locals.Add(type); zeroReferences.Add(type, index); return index;
            }
            var inputs = method.Blocks.Select(b => b.Inputs.Select(Expand).ToArray()).ToArray();
            var inputStarts = inputs.Select(blockInputs =>
            {
                int start = 0; var starts = new int[blockInputs.Length];
                for (int i = 0; i < starts.Length; i++) { starts[i] = start; start += blockInputs[i].Length; }
                return starts;
            }).ToArray();
            var blocks = ImmutableArray.CreateBuilder<IrBlock>();
            foreach (var block in method.Blocks)
            {
                var output = ImmutableArray.CreateBuilder<IrStatement>();
                var values = new List<ImmutableArray<int>>();
                int next = 0;
                void Emit(IrStatement statement)
                {
                    Require(statements < IrLimits.MaximumStatements, "expanded statement limit exceeded.");
                    ++statements; output.Add(statement);
                }
                int Define(IrType type, int offset, IrExpression expression, IrExceptionTarget? exceptional = null)
                {
                    int id = next++; Emit(new IrStatement.Define(new(id, type, offset, expression), exceptional)); return id;
                }
                int One(int value) => values[value].Single();
                ImmutableArray<int> Arguments(ImmutableArray<int> source, bool call)
                {
                    int count = 0;
                    foreach (int argument in source)
                    {
                        int maximum = call ? ProgramLimits.MaximumCallArguments - arguments : IrLimits.MaximumEdgeArguments - edges;
                        Require(values[argument].Length <= maximum - count, "expanded call/edge argument limit exceeded.");
                        count += values[argument].Length;
                    }
                    if (call) { Require(count <= IrLimits.MaximumParameters, "expanded call packet limit exceeded."); arguments += count; }
                    else edges += count;
                    return [.. source.SelectMany(index => values[index])];
                }
                IrEdge Edge(IrEdge edge) => new(edge.Target, Arguments(edge.Arguments, false));
                foreach (var statement in block.Statements)
                {
                    if (statement is IrStatement.StoreLocal store)
                    {
                        for (int i = 0; i < values[store.Value].Length; i++)
                            Emit(new IrStatement.StoreLocal(store.ILOffset, localSlots[store.Local] + i, values[store.Value][i]));
                        continue;
                    }
                    if (statement is IrStatement.Invoke invoke)
                    {
                        Emit(invoke with { Arguments = Arguments(invoke.Arguments, true) }); continue;
                    }
                    if (statement is IrStatement.StoreLocation write)
                    {
                        // All RHS leaves already denote snapshots, including when
                        // source and destination borrow overlapping frame storage.
                        for (int i = 0; i < values[write.Location].Length; ++i)
                            Emit(write with { Location = values[write.Location][i], Value = values[write.Value][i] });
                        continue;
                    }
                    if (statement is IrStatement.InitializeLocation initialize)
                    {
                        foreach (int location in values[initialize.Location]) Emit(initialize with { Location = location });
                        continue;
                    }
                    var definition = (IrStatement.Define)statement;
                    var value = definition.Value; var expanded = Expand(value.Type);
                    var ids = ImmutableArray.CreateBuilder<int>();
                    switch (value.Expression)
                    {
                        case IrExpression.AddressLocal address:
                            for (int i = 0; i < expanded.Length; ++i) ids.Add(Define(expanded[i], value.ILOffset,
                                new IrExpression.AddressLocal(localSlots[address.Index] + i)));
                            break;
                        case IrExpression.AddressParameter address:
                            for (int i = 0; i < expanded.Length; ++i) ids.Add(Define(expanded[i], value.ILOffset,
                                new IrExpression.AddressParameter(parameterSlots[address.Index] + i)));
                            break;
                        case IrExpression.LoadLocation load:
                            for (int i = 0; i < expanded.Length; ++i) ids.Add(Define(expanded[i], value.ILOffset,
                                new IrExpression.LoadLocation(values[load.Location][i]), definition.Exceptional));
                            break;
                        case IrExpression.ReadOnlyLocation view:
                            for (int i = 0; i < expanded.Length; ++i) ids.Add(Define(expanded[i], value.ILOffset,
                                new IrExpression.ReadOnlyLocation(values[view.Location][i])));
                            break;
                        case IrExpression.LocationField field:
                            var selected = ranges[field.Field];
                            for (int i = 0; i < selected.Count; ++i)
                            {
                                int leaf = values[field.Location][selected.Start + i];
                                ids.Add(value.Type.ReadOnly ? Define(expanded[i], value.ILOffset, new IrExpression.ReadOnlyLocation(leaf)) : leaf);
                            }
                            break;
                        case IrExpression.Parameter parameter:
                            for (int i = 0; i < expanded.Length; i++) ids.Add(Define(expanded[i], value.ILOffset, new IrExpression.Parameter(parameterSlots[parameter.Index] + i)));
                            break;
                        case IrExpression.Local local:
                            for (int i = 0; i < expanded.Length; i++) ids.Add(Define(expanded[i], value.ILOffset, new IrExpression.Local(localSlots[local.Index] + i)));
                            break;
                        case IrExpression.StackInput input:
                            int start = inputStarts[block.Id][input.Index];
                            for (int i = 0; i < expanded.Length; i++) ids.Add(Define(expanded[i], value.ILOffset, new IrExpression.StackInput(start + i)));
                            break;
                        case IrExpression.DefaultValue:
                            foreach (var type in expanded) ids.Add(Define(type, value.ILOffset, type == IrType.Int32
                                ? new IrExpression.Constant(0) : new IrExpression.Local(ZeroReference(type))));
                            break;
                        case IrExpression.ValueField field:
                            var range = ranges[field.Field]; ids.AddRange(values[field.Operand].AsSpan(range.Start, range.Count));
                            break;
                        case IrExpression.WithValueField field:
                            var replaced = values[field.Operand].ToArray(); var target = ranges[field.Field];
                            values[field.Value].CopyTo(replaced, target.Start); ids.AddRange(replaced);
                            break;
                        default:
                            IrExpression expression = value.Expression switch
                            {
                                IrExpression.Constant or IrExpression.Null => value.Expression,
                                IrExpression.Binary binary => binary with { Left = One(binary.Left), Right = One(binary.Right) },
                                IrExpression.Unary unary => unary with { Operand = One(unary.Operand) },
                                IrExpression.Compare compare => compare with { Left = One(compare.Left), Right = One(compare.Right) },
                                IrExpression.Call call => call with { Arguments = Arguments(call.Arguments, true) },
                                IrExpression.AddressField field => field with { Receiver = One(field.Receiver) },
                                _ => throw new InvalidOperationException("Unsupported validated value expression."),
                            };
                            ids.Add(Define(value.Type, value.ILOffset, expression, definition.Exceptional));
                            break;
                    }
                    values.Add(ids.ToImmutable());
                }
                IrTerminator terminator = block.Terminator switch
                {
                    IrReturn returned => returned with { Value = One(returned.Value) },
                    IrVoidReturn => block.Terminator,
                    IrJump jump => jump with { Edge = Edge(jump.Edge) },
                    IrBranch branch => branch with { Left = One(branch.Left), Right = branch.Right is { } right ? One(right) : null,
                        Taken = Edge(branch.Taken), NotTaken = Edge(branch.NotTaken) },
                    _ => throw new InvalidOperationException("Unsupported validated value terminator."),
                };
                blocks.Add(new(block.Id, block.ILOffset, [.. inputs[block.Id].SelectMany(types => types)], output.ToImmutable(), terminator));
            }
            lowered.Add(method with { Parameters = parameters.ToImmutable(), Locals = locals.ToImmutable(), Blocks = blocks.ToImmutable() });
        }
        return new(program.Entry, lowered.ToImmutable(), program.Types.References);
    }
}
