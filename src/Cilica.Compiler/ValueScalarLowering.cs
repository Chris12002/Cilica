using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Expands inline copies into scalar/reference IR with explicitly selected object and boxing operations.</summary>
public static class ValueScalarLowering
{
    public const string Profile = "value-scalars-v2";
    public const string LocationProfile = "value-location-scalars-v4";

    public static ScalarManagedExceptionIrProgram LowerExceptions(ManagedExceptionIrProgram program)
    {
        _ = ValueRootAssignment.Assign(program);
        var values = program.Flow.Flow.Flow;
        var objects = new ObjectIrProgram(LowerCore(values, returnPackets: true), LowerBindings(program.Flow.Bindings))
            { InterfaceDispatch = LowerInterfacePlan(program.Flow.InterfaceDispatch), Dispatch = program.Flow.Dispatch, AbstractMethods = program.Flow.AbstractMethods };
        var scalar = new ScalarBoxedIrProgram(new(objects), values.Types);
        var result = new ScalarManagedExceptionIrProgram(new(scalar), program.Exceptions);
        _ = ObjectRootAssignment.AssignExceptions(result);
        return result;
    }

    internal static ImmutableArray<ObjectMethodBinding> LowerBindings(ImmutableArray<ObjectMethodBinding> bindings) =>
        [.. bindings.Select(binding => binding.Kind is ObjectMethodKind.ValueConstructor or ObjectMethodKind.ValueInstance or ObjectMethodKind.PrimitiveInstance ? binding with {
            Kind = ObjectMethodKind.Static, Primitive = null, InvariantProviderOnly = false,
            Attributes = (binding.Attributes & ~(System.Reflection.MethodAttributes.SpecialName | System.Reflection.MethodAttributes.RTSpecialName
                | System.Reflection.MethodAttributes.Virtual | System.Reflection.MethodAttributes.NewSlot | System.Reflection.MethodAttributes.Final))
                | System.Reflection.MethodAttributes.Static } : binding)];

    public static ScalarDelegateIrProgram LowerDelegates(DelegateValueIrProgram program)
    {
        _ = ValueRootAssignment.Assign(program);
        var objects = new ObjectIrProgram(LowerCore(program.Flow.Flow, returnPackets: true), LowerBindings(program.Bindings))
            { InterfaceDispatch = LowerInterfacePlan(program.InterfaceDispatch), Dispatch = program.Dispatch, AbstractMethods = program.AbstractMethods };
        var scalar = new ScalarBoxedIrProgram(new(objects), program.Flow.Flow.Types);
        var result = new ScalarDelegateIrProgram(scalar);
        _ = ObjectRootAssignment.AssignDelegates(result);
        return result;
    }

    public static ScalarBoxedIrProgram LowerBoxes(BoxedValueIrProgram program)
    {
        _ = ValueRootAssignment.Assign(program);
        var result = new ScalarBoxedIrProgram(new(new(LowerCore(program.Flow.Flow, returnPackets: true), LowerBindings(program.Bindings))), program.Flow.Flow.Types);
        _ = ObjectRootAssignment.AssignBoxes(result);
        return result;
    }

    public static ScalarLocationIrProgram LowerArrays(ValueArrayIrProgram program)
    {
        _ = ValueRootAssignment.Assign(program);
        var result = new ScalarLocationIrProgram(new(LowerCore(program.Flow.Flow, returnPackets: true), LowerBindings(program.Bindings)));
        _ = ObjectRootAssignment.AssignLocations(result);
        return result;
    }

    public static ScalarLocationIrProgram LowerObjects(ValueObjectIrProgram program)
    {
        _ = ValueRootAssignment.Assign(program);
        var result = new ScalarLocationIrProgram(new(LowerCore(program.Flow.Flow, returnPackets: true), LowerBindings(program.Bindings)));
        _ = ObjectRootAssignment.AssignLocations(result);
        return result;
    }

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
        var result = new ScalarLocationIrProgram(new(LowerCore(program.Flow, returnPackets: true), bindings));
        _ = ObjectRootAssignment.AssignLocations(result);
        return result;
    }

    internal static ImmutableArray<IrType> ExpandSignatureType(IrType type, IReadOnlyDictionary<ObjectTypeId, ValueTypePlan> plans)
    {
        static IrType Scalar(ObjectStorage storage) => storage switch
        {
            ObjectStorage.Int32 => IrType.Int32,
            ObjectStorage.Int64 => IrType.Int64,
            ObjectStorage.Reference reference => IrType.Reference(reference.Type),
            _ => throw new InvalidOperationException("Validated layout has a non-scalar leaf."),
        };
        return type.Kind switch
        {
            IrTypeKind.Value => [.. plans[type.Class!].Slots.Select(slot => Scalar(slot.Storage))],
            IrTypeKind.ManagedLocation when type.Element!.Kind == IrTypeKind.Value =>
                [.. plans[type.Element.Class!].Slots.Select(slot => IrType.Location(Scalar(slot.Storage), type.ReadOnly))],
            _ => [type],
        };
    }

    private static ObjectInterfaceDispatchPlan? LowerInterfacePlan(ObjectInterfaceDispatchPlan? plan)
    {
        if (plan?.Values is not { } values) return plan;
        var layouts = ValueLayoutPlanner.Build(values).ToDictionary(p => p.Type);
        ImmutableArray<IrType> Parameters(ImmutableArray<IrType> parameters, IrType returned)
        {
            var result = ImmutableArray.CreateBuilder<IrType>();
            void Append(IrType type, bool destination)
            {
                var expanded = ExpandSignatureType(type, layouts);
                if (expanded.Length > IrLimits.MaximumParameters - result.Count)
                    throw new ArgumentException("Value interface signature exceeds the expanded parameter budget.");
                result.AddRange(destination ? expanded.Select(t => IrType.Location(t)) : expanded);
            }
            foreach (var parameter in parameters) Append(parameter, false);
            if (returned.Kind == IrTypeKind.Value) Append(returned, true);
            return result.ToImmutable();
        }
        var lowered = plan with {
            Values = null, BoxedValues = plan.Methods.Any(m => m.BoxedDeclaration is not null) ? values : null, Types = new HeapValueFields(values, layouts).Catalog,
            Declarations = [.. plan.Declarations.Select(d => d with { Parameters = Parameters(d.Parameters, d.ReturnType),
                ReturnType = d.ReturnType.Kind == IrTypeKind.Value ? IrType.Void : d.ReturnType })],
            Methods = [.. plan.Methods.Select(m => m with { Binding = m.BoxedDeclaration is null ? m.Binding : LowerBindings([m.Binding])[0], Parameters = Parameters(m.Parameters, m.ReturnType),
                ReturnType = m.ReturnType.Kind == IrTypeKind.Value ? IrType.Void : m.ReturnType })] };
        ObjectInterfacePlanValidator.Validate(lowered);
        return lowered;
    }

    private static ReferenceIrProgram LowerCore(ValueIrProgram program, bool returnPackets = false)
    {
        var plans = ValueLayoutPlanner.Build(program.Types).ToDictionary(p => p.Type);
        var heap = new HeapValueFields(program.Types, plans);
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
        ImmutableArray<IrType> Expand(IrType type) => ExpandSignatureType(type, plans);
        static void Require(bool condition, string message)
        {
            if (!condition) throw new ArgumentException("Value scalar lowering: " + message, nameof(program));
        }
        var entry = program.Methods.Single(m => IrMethodId.From(m.Source) == program.Entry);
        Require(entry.ReturnType == IrType.Int32 && entry.Parameters.All(p => p == IrType.Int32),
            "the entry must retain the existing packed Int32 boundary.");
        Require(returnPackets || program.Methods.All(m => m.ReturnType.Kind != IrTypeKind.Value),
            "struct return packets require LowerLocations; the pure-copy entry point supports scalar returns only.");
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
            int returnStart = parameters.Count;
            var returnSlots = method.ReturnType.Kind == IrTypeKind.Value ? Expand(method.ReturnType) : [];
            Require(returnSlots.Length <= IrLimits.MaximumParameters - parameters.Count, "expanded return parameter limit exceeded.");
            parameters.AddRange(returnSlots.Select(t => IrType.Location(t)));
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
                ImmutableArray<int> Arguments(ImmutableArray<int> source, bool call, ImmutableArray<int> tail = default)
                {
                    int count = tail.IsDefault ? 0 : tail.Length;
                    foreach (int argument in source)
                    {
                        int maximum = call ? ProgramLimits.MaximumCallArguments - arguments : IrLimits.MaximumEdgeArguments - edges;
                        Require(values[argument].Length <= maximum - count, "expanded call/edge argument limit exceeded.");
                        count += values[argument].Length;
                    }
                    if (call)
                    {
                        Require(count <= IrLimits.MaximumParameters, "expanded call packet limit exceeded.");
                        Require(count <= ProgramLimits.MaximumCallArguments - arguments, "expanded call argument limit exceeded.");
                        arguments += count;
                    }
                    else edges += count;
                    return [.. source.SelectMany(index => values[index]), .. (tail.IsDefault ? ImmutableArray<int>.Empty : tail)];
                }
                IrEdge Edge(IrEdge edge) => new(edge.Target, Arguments(edge.Arguments, false));
                foreach (var statement in block.Statements)
                {
                    if (statement is IrStatement.InitializeType) { Emit(statement); continue; }
                    if (statement is IrStatement.StoreLocal store)
                    {
                        for (int i = 0; i < values[store.Value].Length; i++)
                            Emit(new IrStatement.StoreLocal(store.ILOffset, localSlots[store.Local] + i, values[store.Value][i]));
                        continue;
                    }
                    if (statement is IrStatement.InvokeDelegate delegateCall)
                    {
                        _ = Arguments([delegateCall.Delegate], true);
                        Emit(delegateCall with { Delegate = One(delegateCall.Delegate), Arguments = Arguments(delegateCall.Arguments, true) }); continue;
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
                    if (statement is IrStatement.StoreElement element)
                    {
                        if (element.ArrayType.ArrayElement!.Kind == ObjectArrayElementKind.Value)
                        {
                            var shape = heap.Catalog.ValueArrays.Single(a => a.Type == element.ArrayType.ArrayElement.Type);
                            var elementTypes = Expand(IrType.Value(shape.Type));
                            for (int i = 0; i < shape.Slots.Length; ++i)
                            {
                                int source = values[element.Value].IsEmpty ? Define(IrType.Int32, element.ILOffset, new IrExpression.Constant(0)) : values[element.Value][i];
                                var scalarType = values[element.Value].IsEmpty ? IrType.Int32 : elementTypes[i];
                                int address = Define(IrType.Location(scalarType), element.ILOffset,
                                    new IrExpression.AddressArraySlot(element.ArrayType, One(element.Array), One(element.Index), i), element.Exceptional);
                                Emit(new IrStatement.StoreLocation(element.ILOffset, address, source, element.Exceptional));
                            }
                        }
                        else Emit(element with { Array = One(element.Array), Index = One(element.Index), Value = One(element.Value) });
                        continue;
                    }
                    if (statement is IrStatement.StoreStatic staticStore)
                    {
                        if (heap.Statics.TryGetValue(staticStore.Field, out var components))
                        {
                            var sources = values[staticStore.Value];
                            for (int i = 0; i < components.Length; ++i)
                            {
                                int source = sources.IsEmpty ? Define(IrType.Int32, staticStore.ILOffset, new IrExpression.Constant(0)) : sources[i];
                                Emit(staticStore with { Field = components[i].Id, Value = source });
                            }
                        }
                        else Emit(staticStore with { Value = One(staticStore.Value) });
                        continue;
                    }
                    if (statement is IrStatement.StoreField fieldStore)
                    {
                        if (heap.Fields.TryGetValue(fieldStore.Field, out var components))
                        {
                            var sources = values[fieldStore.Value];
                            for (int i = 0; i < components.Length; ++i)
                            {
                                int source = sources.IsEmpty ? Define(IrType.Int32, fieldStore.ILOffset, new IrExpression.Constant(0)) : sources[i];
                                Emit(fieldStore with { Field = components[i].Id, Receiver = One(fieldStore.Receiver), Value = source });
                            }
                        }
                        else Emit(fieldStore with { Receiver = One(fieldStore.Receiver), Value = One(fieldStore.Value) });
                        continue;
                    }
                    var definition = (IrStatement.Define)statement;
                    if (definition.Value.Expression is IrExpression.NewDelegate capture) _ = Arguments([capture.Receiver], true);
                    if (definition.Value.Expression is IrExpression.CallDelegate delegateCallExpression) _ = Arguments([delegateCallExpression.Delegate], true);
                    var value = definition.Value; var expanded = Expand(value.Type);
                    var ids = ImmutableArray.CreateBuilder<int>();
                    switch (value.Expression)
                    {
                        case IrExpression.LoadStatic field when heap.Statics.TryGetValue(field.Field, out var loadedStatics):
                            for (int i = 0; i < loadedStatics.Length; ++i)
                            {
                                int loaded = Define(expanded.IsEmpty ? IrType.Int32 : expanded[i], value.ILOffset,
                                    new IrExpression.LoadStatic(loadedStatics[i].Id), definition.Exceptional);
                                if (!expanded.IsEmpty) ids.Add(loaded);
                            }
                            break;
                        case IrExpression.LoadElement vectorElement when vectorElement.ArrayType.ArrayElement!.Kind == ObjectArrayElementKind.Value:
                            for (int i = 0; i < Math.Max(1, expanded.Length); ++i)
                            {
                                var scalar = expanded.IsEmpty ? IrType.Int32 : expanded[i];
                                int location = Define(IrType.Location(scalar), value.ILOffset,
                                    new IrExpression.AddressArraySlot(vectorElement.ArrayType, One(vectorElement.Array), One(vectorElement.Index), i), definition.Exceptional);
                                int loaded = Define(scalar, value.ILOffset, new IrExpression.LoadLocation(location), definition.Exceptional);
                                if (!expanded.IsEmpty) ids.Add(loaded);
                            }
                            break;
                        case IrExpression.AddressElement vectorElement:
                            for (int i = 0; i < Math.Max(1, expanded.Length); ++i)
                            {
                                int location = Define(expanded.IsEmpty ? IrType.Location(IrType.Int32, value.Type.ReadOnly) : expanded[i], value.ILOffset,
                                    new IrExpression.AddressArraySlot(vectorElement.ArrayType, One(vectorElement.Array), One(vectorElement.Index), i), definition.Exceptional);
                                if (!expanded.IsEmpty) ids.Add(location);
                            }
                            break;
                        case IrExpression.LoadField field when heap.Fields.ContainsKey(field.Field):
                            var loadedFields = heap.Fields[field.Field];
                            for (int i = 0; i < loadedFields.Length; ++i)
                            {
                                int loaded = Define(expanded.IsEmpty ? IrType.Int32 : expanded[i], value.ILOffset,
                                    new IrExpression.LoadField(loadedFields[i].Id, One(field.Receiver)), definition.Exceptional);
                                if (!expanded.IsEmpty) ids.Add(loaded);
                            }
                            break;
                        case IrExpression.AddressField field when heap.Fields.ContainsKey(field.Field):
                            var addressedFields = heap.Fields[field.Field];
                            for (int i = 0; i < addressedFields.Length; ++i)
                            {
                                int address = Define(expanded.IsEmpty ? IrType.Location(IrType.Int32, value.Type.ReadOnly) : expanded[i], value.ILOffset,
                                    new IrExpression.AddressField(addressedFields[i].Id, One(field.Receiver)), definition.Exceptional);
                                if (!expanded.IsEmpty) ids.Add(address);
                            }
                            break;
                        case IrExpression.BoxValue boxed:
                            var boxArguments = Arguments([boxed.Operand], true);
                            if (plans[boxed.Type].ScalarBox is { } scalarBox
                                && ScalarBoxing.Narrow(scalarBox) is { } narrowBox)
                                boxArguments = [Define(IrType.Int32, value.ILOffset, new IrExpression.IntegerCast(narrowBox, boxArguments[0], false, false))];
                            ids.Add(Define(value.Type, value.ILOffset, new IrExpression.BoxScalars(boxed.Type,
                                boxArguments), definition.Exceptional));
                            break;
                        case IrExpression.UnboxValue or IrExpression.UnboxLocation:
                            bool borrow = value.Expression is IrExpression.UnboxLocation;
                            var boxType = value.Expression is IrExpression.UnboxValue copied ? copied.Type : ((IrExpression.UnboxLocation)value.Expression).Type;
                            int boxOperand = value.Expression is IrExpression.UnboxValue copy ? copy.Operand : ((IrExpression.UnboxLocation)value.Expression).Operand;
                            // Keep a rooted checked receiver even when expansion has zero
                            // leaves. Its owning frame stays live across synchronous calls.
                            int owner = Define(IrType.Reference(program.Types.References.ObjectBase), value.ILOffset,
                                new IrExpression.CheckBox(boxType, One(boxOperand)) { NullableTarget = value.Expression is IrExpression.UnboxValue { NullableTarget: true } }, definition.Exceptional);
                            for (int i = 0; i < expanded.Length; ++i)
                            {
                                int location = Define(borrow ? expanded[i] : IrType.Location(expanded[i]), value.ILOffset,
                                    new IrExpression.AddressBoxSlot(boxType, owner, i), definition.Exceptional);
                                int loaded = borrow ? location : Define(expanded[i], value.ILOffset, new IrExpression.LoadLocation(location), definition.Exceptional);
                                if (!borrow && plans[boxType].ScalarBox is { } scalarUnbox
                                    && ScalarBoxing.Narrow(scalarUnbox) is { } narrowUnbox)
                                    loaded = Define(IrType.Int32, value.ILOffset, new IrExpression.IntegerCast(narrowUnbox, loaded, false, false));
                                ids.Add(loaded);
                            }
                            break;
                        case IrExpression.Call or IrExpression.CallDelegate when value.Type.Kind == IrTypeKind.Value:
                            // Caller-owned typed result slots are rooted before the
                            // callee starts; no pointer into a popped frame returns.
                            Require(expanded.Length <= IrLimits.MaximumLocals - locals.Count, "return packet local limit exceeded.");
                            int resultStart = locals.Count; locals.AddRange(expanded);
                            var destinations = ImmutableArray.CreateBuilder<int>();
                            for (int i = 0; i < expanded.Length; ++i)
                                destinations.Add(Define(IrType.Location(expanded[i]), value.ILOffset, new IrExpression.AddressLocal(resultStart + i)));
                            if (value.Expression is IrExpression.Call directResultCall)
                                Emit(new IrStatement.Invoke(value.ILOffset, directResultCall.Target, Arguments(directResultCall.Arguments, true, destinations.ToImmutable()),
                                    NullCheckReceiver: directResultCall.NullCheckReceiver, VirtualDispatch: directResultCall.VirtualDispatch));
                            else
                            {
                                var callable = (IrExpression.CallDelegate)value.Expression;
                                Emit(new IrStatement.InvokeDelegate(value.ILOffset, callable.Type, One(callable.Delegate), Arguments(callable.Arguments, true, destinations.ToImmutable())));
                            }
                            for (int i = 0; i < expanded.Length; ++i)
                                ids.Add(Define(expanded[i], value.ILOffset, new IrExpression.LoadLocation(destinations[i]), IrExceptionTarget.MethodExit));
                            break;
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
                                ? new IrExpression.Constant(0) : type == IrType.Int64 ? new IrExpression.Constant64(0) : new IrExpression.Local(ZeroReference(type))));
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
                                IrExpression.Constant or IrExpression.Constant64 or IrExpression.Null or IrExpression.StringLiteral or IrExpression.InvariantCulture or IrExpression.RuntimeTypeLiteral => value.Expression,
                                IrExpression.EnumEntry enumEntry => enumEntry with { Index = One(enumEntry.Index) },
                                IrExpression.EnumQuery query => query with { Type = One(query.Type), Value = One(query.Value) },
                                IrExpression.Binary binary => binary with { Left = One(binary.Left), Right = One(binary.Right) },
                                IrExpression.Binary64 binary => binary with { Left = One(binary.Left), Right = One(binary.Right) },
                                IrExpression.Not64 unary => unary with { Operand = One(unary.Operand) },
                                IrExpression.ConvertInteger conversion => conversion with { Operand = One(conversion.Operand) },
                                IrExpression.IntegerCast cast => cast with { Operand = One(cast.Operand) },
                                IrExpression.Unary unary => unary with { Operand = One(unary.Operand) },
                                IrExpression.Compare compare => compare with { Left = One(compare.Left), Right = One(compare.Right) },
                                IrExpression.Call call => call with { Arguments = Arguments(call.Arguments, true) },
                                IrExpression.AddressField field => field with { Receiver = One(field.Receiver) },
                                IrExpression.NewException e => e with { Message = e.Message is int message ? One(message) : null, ParameterName = e.ParameterName is int parameter ? One(parameter) : null, Inner = e.Inner is int inner ? One(inner) : null, TypeName = e.TypeName is int name ? One(name) : null, ActualValue = e.ActualValue is int actual ? One(actual) : null },
                                IrExpression.ExceptionInnerException e => e with { Operand = One(e.Operand) },
                                IrExpression.ExceptionTypeName e => e with { Operand = One(e.Operand) },
                                IrExpression.ExceptionParameterName e => e with { Operand = One(e.Operand) },
                                IrExpression.ExceptionMessage e => e with { Operand = One(e.Operand) },
                                IrExpression.IsInstance e => e with { Operand = One(e.Operand) },
                                IrExpression.CastClass e => e with { Operand = One(e.Operand) },
                                IrExpression.StringLength e => e with { Operand = One(e.Operand) },
                                IrExpression.StringCodeUnit e => e with { Operand = One(e.Operand), Index = One(e.Index) },
                                IrExpression.ObjectGetType e => e with { Operand = One(e.Operand) },
                                IrExpression.ObjectIdentityHash e => e with { Operand = One(e.Operand) },
                                IrExpression.RuntimeTypeHash e => e with { Operand = One(e.Operand) },
                                IrExpression.IntegerTextFind e => e with { Index = One(e.Index) },
                                IrExpression.IntegerTextPublish e => e with { Index = One(e.Index), Text = One(e.Text) },
                                IrExpression.StringFromCodeUnits e => e with { Array = One(e.Array) },
                                IrExpression.StringConcat e => e with { Left = One(e.Left), Right = One(e.Right) },
                                IrExpression.StringEquals e => e with { Left = One(e.Left), Right = One(e.Right) },
                                IrExpression.NewDelegate d => d with { Receiver = One(d.Receiver) },
                                IrExpression.CallDelegate d => d with { Delegate = One(d.Delegate), Arguments = Arguments(d.Arguments, true) },
                                IrExpression.LoadStatic staticRead => staticRead,
                                IrExpression.EmptyArray empty => empty,
                                IrExpression.NewArray array => array with { Length = One(array.Length) },
                                IrExpression.NewObject allocated => allocated with { Arguments = Arguments(allocated.Arguments, true) },
                                IrExpression.LoadField loaded => loaded with { Receiver = One(loaded.Receiver) },
                                IrExpression.ArrayCopyCompatible copyCheck => copyCheck with { Source = One(copyCheck.Source), Destination = One(copyCheck.Destination) },
                                IrExpression.ArrayLength length => length with { Array = One(length.Array) },
                                IrExpression.LoadElement loadElement => loadElement with { Array = One(loadElement.Array), Index = One(loadElement.Index) },
                                _ => throw new InvalidOperationException("Unsupported validated value expression."),
                            };
                            ids.Add(Define(value.Type, value.ILOffset, expression, definition.Exceptional));
                            break;
                    }
                    values.Add(ids.ToImmutable());
                }
                if (block.Terminator is IrReturn wholeReturn && method.ReturnType.Kind == IrTypeKind.Value)
                    for (int i = 0; i < returnSlots.Length; ++i)
                    {
                        int destination = Define(IrType.Location(returnSlots[i]), wholeReturn.ILOffset, new IrExpression.Parameter(returnStart + i));
                        Emit(new IrStatement.StoreLocation(wholeReturn.ILOffset, destination, values[wholeReturn.Value][i]));
                    }
                IrTerminator terminator = block.Terminator switch
                {
                    IrReturn returned when method.ReturnType.Kind == IrTypeKind.Value => new IrVoidReturn(returned.ILOffset),
                    IrReturn returned => returned with { Value = One(returned.Value) },
                    IrVoidReturn or IrRethrow or IrEndFinally => block.Terminator,
                    IrThrow thrown => thrown with { Value = One(thrown.Value) },
                    IrEndFilter ended => ended with { Value = One(ended.Value) },
                    IrLeave leave => leave with { Edge = Edge(leave.Edge) },
                    IrJump jump => jump with { Edge = Edge(jump.Edge) },
                    IrBranch branch => branch with { Left = One(branch.Left), Right = branch.Right is { } right ? One(right) : null,
                        Taken = Edge(branch.Taken), NotTaken = Edge(branch.NotTaken) },
                    _ => throw new InvalidOperationException("Unsupported validated value terminator."),
                };
                blocks.Add(new(block.Id, block.ILOffset, [.. inputs[block.Id].SelectMany(types => types)], output.ToImmutable(), terminator));
            }
            lowered.Add(method with { Parameters = parameters.ToImmutable(), Locals = locals.ToImmutable(), Blocks = blocks.ToImmutable(),
                ReturnType = method.ReturnType.Kind == IrTypeKind.Value ? IrType.Void : method.ReturnType });
        }
        return new(program.Entry, lowered.ToImmutable(), heap.Catalog);
    }
}
