using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

public sealed record ManagedExceptionTranslationResult(ManagedExceptionIrProgram? Program, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Program is not null;
}
public sealed record ValueTranslationResult(ManagedLocationIrProgram? Program,
    ImmutableArray<ObjectMethodBinding> Bindings, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Program is not null;
    public ManagedExceptionTypes? Exceptions { get; init; }
    public ObjectInterfaceDispatchPlan? InterfaceDispatch { get; init; }
    public ImmutableArray<ObjectVirtualDispatch> Dispatch { get; init; } = [];
}

public sealed record ValueArrayTranslationResult(ValueArrayIrProgram? Program, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Program is not null;
}
public sealed record ValueObjectTranslationResult(ValueObjectIrProgram? Program, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Program is not null;
}
public sealed record BoxedValueTranslationResult(BoxedValueIrProgram? Program, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Program is not null;
}

public sealed record DelegateValueTranslationResult(DelegateValueIrProgram? Program, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Program is not null;
}

/// <summary>Translates bounded static CIL value operations into the shared copy/borrow IR.</summary>
public static class ValueTranslator
{
    public const string IntegerObjectProfile = "integer-managed-objects-cil-v1";
    public static ManagedExceptionTranslationResult TranslateIntegerObjects(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits = null)
    {
        var result = TranslateCore(resolver, assembly, entry, runtimeLibrary, limits, arrays: true, classes: true, boxing: true, generics: true, delegates: true, exceptions: true, integers64: true);
        return new(result.Program is null ? null : new(new(result.Program, result.Bindings) { InterfaceDispatch = result.InterfaceDispatch, Dispatch = result.Dispatch }, result.Exceptions!), result.Diagnostics);
    }

    public const string ExceptionProfile = "managed-exceptions-cil-v1";
    public static ManagedExceptionTranslationResult TranslateExceptions(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits = null)
    {
        var result = TranslateCore(resolver, assembly, entry, runtimeLibrary, limits, arrays: true, classes: true, boxing: true, generics: true, delegates: true, exceptions: true);
        return new(result.Program is null ? null : new(new(result.Program, result.Bindings), result.Exceptions!), result.Diagnostics);
    }

    public const string Profile = "value-cil-v1";
    public const string ArrayProfile = "value-arrays-cil-v1";
    public const string ObjectProfile = "value-objects-cil-v1";
    public const string GenericProfile = "closed-generic-values-cil-v1";
    public const string DelegateProfile = "delegate-values-cil-v2";
    public const string BoxedProfile = "boxed-values-cil-v1";

    public static DelegateValueTranslationResult TranslateDelegates(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits = null)
    {
        var result = TranslateCore(resolver, assembly, entry, runtimeLibrary, limits, arrays: true, classes: true, boxing: true, generics: true, delegates: true);
        return new(result.Program is null ? null : new(result.Program, result.Bindings), result.Diagnostics);
    }

    public static BoxedValueTranslationResult TranslateGenerics(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits = null)
    {
        var result = TranslateCore(resolver, assembly, entry, runtimeLibrary, limits, arrays: true, classes: true, boxing: true, generics: true);
        return new(result.Program is null ? null : new(result.Program, result.Bindings), result.Diagnostics);
    }

    public static BoxedValueTranslationResult TranslateBoxes(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits = null)
    {
        var result = TranslateCore(resolver, assembly, entry, runtimeLibrary, limits, arrays: true, classes: true, boxing: true);
        return new(result.Program is null ? null : new(result.Program, result.Bindings), result.Diagnostics);
    }

    public static ValueObjectTranslationResult TranslateObjects(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits = null)
    {
        var result = TranslateCore(resolver, assembly, entry, runtimeLibrary, limits, arrays: true, classes: true);
        return new(result.Program is null ? null : new(result.Program, result.Bindings), result.Diagnostics);
    }

    public static ValueArrayTranslationResult TranslateArrays(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits = null)
    {
        var result = TranslateCore(resolver, assembly, entry, runtimeLibrary, limits, arrays: true);
        return new(result.Program is null ? null : new(result.Program, result.Bindings), result.Diagnostics);
    }

    public static ValueTranslationResult Translate(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits = null) =>
        TranslateCore(resolver, assembly, entry, runtimeLibrary, limits, arrays: false);

    private static ValueTranslationResult TranslateCore(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits, bool arrays, bool classes = false, bool boxing = false, bool generics = false, bool delegates = false, bool exceptions = false, bool integers64 = false)
    {
        var discovered = integers64 ? ValueProgramDiscovery.ReadIntegerObjects(resolver, assembly, entry, runtimeLibrary, limits)
            : exceptions ? ValueProgramDiscovery.ReadExceptions(resolver, assembly, entry, runtimeLibrary, limits)
            : delegates ? ValueProgramDiscovery.ReadDelegates(resolver, assembly, entry, runtimeLibrary, limits)
            : generics ? ValueProgramDiscovery.ReadGenerics(resolver, assembly, entry, runtimeLibrary, limits)
            : boxing ? ValueProgramDiscovery.ReadBoxes(resolver, assembly, entry, runtimeLibrary, limits)
            : classes ? ValueProgramDiscovery.ReadObjects(resolver, assembly, entry, runtimeLibrary, limits)
            : arrays ? ValueProgramDiscovery.ReadArrays(resolver, assembly, entry, runtimeLibrary, limits)
            : ValueProgramDiscovery.Read(resolver, assembly, entry, runtimeLibrary, limits);
        if (discovered.Input is not { } input) return new(null, [], discovered.Diagnostics);
        try
        {
            var methods = input.Methods.ToDictionary(m => m.Binding.Method);
            var fields = input.Types.Types.SelectMany(t => t.Fields).Concat(classes
                ? input.Types.References.Types.SelectMany(t => t.Fields) : []).ToDictionary(f => f.Id);
            var types = new IrTypeRules(input.Types.References, input.Types, locations: true);
            var boxedRelations = integers64 ? new BoxedTypeRelations(input.Types) : null;
            var resources = new ProgramResources(); var translated = new List<IrMethod>();
            foreach (var method in input.Methods)
            {
                var body = new Body(method, methods, fields, arrays, classes, boxing, input.Types.References.Delegates.ToDictionary(d => ObjectTypeId.From(d.Source)), types, input.Types.References.ObjectBase,
                    input.Types.References.Arrays.FirstOrDefault(a => a.ArrayElement!.Kind == ObjectArrayElementKind.Int32), input.Exceptions, input.Types.References.String, boxedRelations).Translate();
                if (!resources.Add(body)) Fail(method, 0, "Aggregate value IR resource limit exceeded.", DiagnosticIds.TranslationLimit);
                translated.Add(body);
            }
            var program = new ManagedLocationIrProgram(new(input.Entry, [.. translated], input.Types));
            if (integers64) program = RetainUsedVectors(program);
            var interfacePlan = input.InterfaceDispatch is null ? null : input.InterfaceDispatch with { Types = program.Flow.Types.References, Values = input.InterfaceDispatch.Values is null ? null : program.Flow.Types };
            ImmutableArray<ObjectMethodBinding> bindings = [.. input.Methods.Select(m => m.Binding)];
            try
            {
                if (exceptions) _ = ValueRootAssignment.Assign(new ManagedExceptionIrProgram(new(program, bindings) { InterfaceDispatch = interfacePlan, Dispatch = input.Dispatch }, input.Exceptions!));
                else if (delegates) _ = ValueRootAssignment.Assign(new DelegateValueIrProgram(program, bindings));
                else if (boxing) _ = ValueRootAssignment.Assign(new BoxedValueIrProgram(program, bindings));
                else if (classes) _ = ValueRootAssignment.Assign(new ValueObjectIrProgram(program, bindings));
                else if (arrays) _ = ValueRootAssignment.Assign(new ValueArrayIrProgram(program, bindings));
                else _ = ValueRootAssignment.Assign(program);
            }
            catch (ArgumentException error) { Fail(methods[input.Entry], 0, error.Message); }
            return new(program, bindings, []) { Exceptions = input.Exceptions, InterfaceDispatch = interfacePlan, Dispatch = input.Dispatch };
        }
        catch (Failure failure) { return new(null, [], [failure.Diagnostic]); }
    }

    private static ManagedLocationIrProgram RetainUsedVectors(ManagedLocationIrProgram program)
    {
        // Discovery includes fallback views for untyped null element operations.
        // Keep only views actually used after stack translation; nominal types
        // and every declared field remain subject to the normal closure check.
        var flow = program.Flow;
        var used = new HashSet<ObjectTypeId>();
        void Include(ObjectTypeId? type) { if (type?.ArrayElement is not null) used.Add(type); }
        foreach (var method in flow.Methods)
        {
            foreach (var type in method.Parameters.Concat(method.Locals).Append(method.ReturnType)
                .Concat(method.Blocks.SelectMany(b => b.Inputs)).Concat(method.Blocks.SelectMany(b => b.Values).Select(v => v.Type)))
                Include(type.Kind == IrTypeKind.ManagedLocation ? type.Element?.Class : type.Class);
            foreach (var statement in method.Blocks.SelectMany(b => b.Statements))
                switch (statement)
                {
                    case IrStatement.Define { Value.Expression: IrExpression.LoadElement load }: Include(load.ArrayType); break;
                    case IrStatement.StoreElement store: Include(store.ArrayType); break;
                }
        }
        foreach (var field in flow.Types.Types.SelectMany(t => t.Fields).Concat(flow.Types.References.Types.SelectMany(t => t.Fields)))
            if (field.Storage is ObjectStorage.Reference reference) Include(reference.Type);
        foreach (var argument in flow.Types.References.Interfaces.SelectMany(i => i.Arguments)) Include(argument.Reference);
        foreach (var field in flow.Types.References.Statics) Include(field.Type.Class);
        foreach (var declaration in flow.Types.References.Delegates) foreach (var parameter in declaration.Parameters) Include(parameter.Class);
        var catalog = flow.Types.References;
        return program with { Flow = flow with { Types = flow.Types with { References = catalog with {
            Arrays = [.. catalog.Arrays.Where(used.Contains)], Roots = [.. catalog.Roots.Where(t => t.ArrayElement is null || used.Contains(t))] } } } };
    }

    private sealed class Failure(Diagnostic diagnostic) : Exception(diagnostic.Message)
    {
        internal Diagnostic Diagnostic { get; } = diagnostic;
    }
    private static void Fail(ValueMethodInput method, int offset, string message, string code = DiagnosticIds.ExecutionUnsupported) =>
        throw new Failure(new(code, DiagnosticSeverity.Error, message,
            method.Input.Source.Name + " token 0x" + method.Input.Source.Token.ToString("x8", CultureInfo.InvariantCulture)
            + " IL_" + offset.ToString("x4", CultureInfo.InvariantCulture)));

    private sealed class Body(ValueMethodInput method, Dictionary<IrMethodId, ValueMethodInput> methods,
        Dictionary<ObjectFieldId, ObjectField> fields, bool arrays, bool classes, bool boxing, Dictionary<ObjectTypeId, ObjectDelegateType> delegates, IrTypeRules types, ObjectTypeId objectBase, ObjectTypeId? integerArray, ManagedExceptionTypes? exceptions, ObjectTypeId? stringType, BoxedTypeRelations? boxedRelations)
    {
        private int _statements, _inputs, _edges;
        private void Error(int offset, string message, string code = DiagnosticIds.InconsistentStack) => Fail(method, offset, message, code);
        internal IrMethod Translate()
        {
            var input = method.Input;
            var locals = method.Locals.ToBuilder();
            var constructorTemporaries = new Dictionary<int, int>();
            var constrainedPrefixes = method.ConstrainedCalls.Values.Select(c => c.PrefixOffset).ToHashSet();
            if (input.Instructions.IsEmpty) Error(0, "Method body is empty.");
            var boundaries = method.ExceptionRegions.SelectMany(r => new[] { r.TryStart, r.TryEnd, r.HandlerStart, r.HandlerEnd, r.FilterStart ?? r.HandlerStart });
            var partition = CilControlFlow.Partition(input, (code, message, offset) => Error(offset, message, code), boundaries, exceptions is not null);
            int BlockAt(int offset) => offset == input.CodeSize ? partition.Ranges.Count : partition.BlockAt[offset];
            var regions = method.ExceptionRegions.Select(r => r with { TryStart = BlockAt(r.TryStart), TryEnd = BlockAt(r.TryEnd),
                HandlerStart = BlockAt(r.HandlerStart), HandlerEnd = BlockAt(r.HandlerEnd), FilterStart = r.FilterStart is int first ? BlockAt(first) : null }).ToImmutableArray();
            var incoming = new ImmutableArray<IrType>[partition.Ranges.Count];
            var incomingThis = new ImmutableArray<bool>[partition.Ranges.Count];
            var blocks = new IrBlock[partition.Ranges.Count]; var pending = new Queue<int>();
            incoming[0] = []; incomingThis[0] = []; pending.Enqueue(0);
            foreach (var region in regions)
            {
                Seed(region.HandlerStart, region.Kind == IrExceptionClause.Catch ? [IrType.Reference(region.CatchType!)]
                    : region.Kind == IrExceptionClause.Filter ? [IrType.Reference(exceptions!.Base)] : []);
                if (region.FilterStart is int filter) Seed(filter, [IrType.Reference(exceptions!.Base)]);
            }
            void Seed(int id, ImmutableArray<IrType> types)
            {
                if (!incoming[id].IsDefault) { if (!incoming[id].SequenceEqual(types)) Error(0, "Conflicting exception entry stack."); return; }
                incoming[id] = types; incomingThis[id] = [.. types.Select(_ => false)]; pending.Enqueue(id);
            }
            while (pending.TryDequeue(out int id)) blocks[id] = Block(id);
            for (int id = 0; id < blocks.Length; id++)
                if (blocks[id] is null) Error(input.Instructions[partition.Ranges[id].Start].Offset,
                    "Unreachable blocks require later analysis; remove dead IL before value translation.", DiagnosticIds.ExecutionUnsupported);
            return new(input.Source, method.Parameters, method.ReturnType, [.. blocks], locals.ToImmutable()) { ExceptionRegions = regions };

            IrBlock Block(int id)
            {
                var (start, end) = partition.Ranges[id]; int blockOffset = input.Instructions[start].Offset;
                _inputs += incoming[id].Length;
                if (_inputs > IrLimits.MaximumInputs) Error(blockOffset, "Aggregate block-input limit exceeded.", DiagnosticIds.TranslationLimit);
                var statements = new List<IrStatement>(); var values = new List<IrType>(); var valueExpressions = new List<IrExpression>(); var stack = new List<int>();
                IrTerminator? terminator = null;
                int transferred = 0;
                for (int i = 0; i < incoming[id].Length; i++)
                    Push(blockOffset, incoming[id][i], incomingThis[id][i] ? new IrExpression.Parameter(0) : new IrExpression.StackInput(transferred++));
                for (int index = start; index < end; index++)
                {
                    var instruction = input.Instructions[index]; int offset = instruction.Offset; string opcode = instruction.OpCode;
                    if (method.Initializers.TryGetValue(offset, out var initializerTarget)) Add(new IrStatement.InitializeType(offset, initializerTarget), offset);
                    if (Int32Operations.FromOpcode(opcode) is { } operation)
                    {
                        int right = Pop(offset), left = Pop(offset);
                        bool shift = operation is Int32Operation.ShiftLeft or Int32Operation.ShiftRightSigned or Int32Operation.ShiftRightUnsigned;
                        if (!Integer(values[left]) || values[right] != (shift ? IrType.Int32 : values[left]))
                            Error(offset, "Integer arithmetic requires matching widths and Int32 shift counts.");
                        Push(offset, values[left], values[left] == IrType.Int64
                            ? new IrExpression.Binary64(Int64Operations.FromOpcode(opcode), left, right)
                            : new IrExpression.Binary(operation, left, right), Int32Operations.MayFault(operation));
                    }
                    else if (types.IntegerCasts && IntegerCasts.FromOpcode(opcode, -1) is { } cast)
                    {
                        int operand = Pop(offset);
                        if (!Integer(values[operand])) Error(offset, "Integer conversion requires an integer stack value.");
                        Push(offset, IntegerCasts.Result(cast.Target), cast with { Operand = operand }, cast.Checked);
                    }
                    else switch (opcode)
                    {
                        case "nop": break;
                        case "constrained." when types.IntegerCasts && constrainedPrefixes.Contains(offset): break;
                        case "ldsfld" when exceptions is not null && method.Literals.ContainsKey(offset):
                        case "ldstr" when exceptions is not null:
                            Push(offset, IrType.Reference(stringType!), new IrExpression.StringLiteral(method.Literals[offset]), true); break;
                        case "isinst" or "castclass" when exceptions is not null:
                            int tested = Pop(offset); var testedType = method.TypeOperands[offset];
                            if (!Reference(values[tested])) Error(offset, "Type tests require a reference operand.");
                            Push(offset, IrType.Reference(testedType), opcode == "isinst" ? new IrExpression.IsInstance(testedType, tested)
                                : new IrExpression.CastClass(testedType, tested), true); break;
                        case "throw" when exceptions is not null:
                            int thrown = Pop(offset, IrType.Reference(exceptions.Base));
                            // CLI throw discards the remainder of the evaluation stack.
                            stack.Clear();
                            terminator = new IrThrow(offset, thrown); break;
                        case "rethrow" when exceptions is not null:
                            if (stack.Count != 0) Error(offset, "rethrow requires an empty evaluation stack.");
                            terminator = new IrRethrow(offset); break;
                        case "leave" or "leave.s" when exceptions is not null:
                            stack.Clear(); terminator = new IrLeave(offset, Edge(instruction.Targets[0], offset)); break;
                        case "endfinally" when exceptions is not null:
                            if (stack.Count != 0) Error(offset, "endfinally requires an empty evaluation stack.");
                            terminator = new IrEndFinally(offset); break;
                        case "endfilter" when exceptions is not null:
                            int accepted = Pop(offset, IrType.Int32);
                            if (stack.Count != 0) Error(offset, "endfilter leaves a residual evaluation stack.");
                            terminator = new IrEndFilter(offset, accepted); break;
                        case "call" when method.KeyOperations.TryGetValue(offset, out var keyOperation):
                            if (keyOperation.Operation == ClosedKeyOperation.IsNull)
                            {
                                int key = Pop(offset, keyOperation.Storage);
                                if (keyOperation.Storage.Kind == IrTypeKind.Reference)
                                {
                                    int nullKey = Define(offset, IrType.NullReference, new IrExpression.Null());
                                    Push(offset, IrType.Int32, new IrExpression.Compare(Int32Comparison.Equal, key, nullKey));
                                }
                                else Push(offset, IrType.Int32, new IrExpression.Constant(0));
                            }
                            else
                            {
                                int rightKey = Pop(offset, keyOperation.Storage);
                                int leftAddress = Pop(offset, IrType.Location(keyOperation.Storage));
                                int leftKey = Define(offset, keyOperation.Storage, new IrExpression.LoadLocation(leftAddress), true);
                                Push(offset, IrType.Int32, new IrExpression.Compare(Int32Comparison.Equal, leftKey, rightKey));
                            }
                            break;
                        case "newobj" when method.Intrinsics.TryGetValue(offset, out var constructorIntrinsic) && constructorIntrinsic.Kind == ValueIntrinsic.NewException:
                            var constructorArguments = new int[constructorIntrinsic.Parameters];
                            for (int i = constructorArguments.Length - 1; i >= 0; --i) constructorArguments[i] = Pop(offset, IrType.Reference(stringType!));
                            Push(offset, IrType.Reference(constructorIntrinsic.Type), new IrExpression.NewException(constructorIntrinsic.Type,
                                constructorIntrinsic.MessageArgument < 0 ? null : constructorArguments[constructorIntrinsic.MessageArgument],
                                constructorIntrinsic.ParameterArgument < 0 ? null : constructorArguments[constructorIntrinsic.ParameterArgument]), true);
                            break;
                        case "newobj" when method.Intrinsics.TryGetValue(offset, out var initialization) && initialization.Kind == ValueIntrinsic.NewTypeInitializationException:
                            int innerException = Pop(offset, IrType.Reference(exceptions!.Base));
                            int initializedName = Pop(offset, IrType.Reference(stringType!));
                            Push(offset, IrType.Reference(initialization.Type), new IrExpression.NewException(initialization.Type, Inner: innerException, TypeName: initializedName), true); break;
                        case "callvirt" when method.Intrinsics.TryGetValue(offset, out var exceptionRead) && exceptionRead.Kind is ValueIntrinsic.ExceptionInnerException or ValueIntrinsic.ExceptionTypeName:
                            int readException = Pop(offset, IrType.Reference(exceptionRead.Type));
                            Push(offset, IrType.Reference(exceptionRead.Kind == ValueIntrinsic.ExceptionInnerException ? exceptions!.Base : stringType!),
                                exceptionRead.Kind == ValueIntrinsic.ExceptionInnerException ? new IrExpression.ExceptionInnerException(readException) : new IrExpression.ExceptionTypeName(readException), true); break;
                        case "call" when method.Intrinsics.TryGetValue(offset, out var ordinal) && ordinal.Kind == ValueIntrinsic.StringOrdinalEqual:
                            int mode = Pop(offset, IrType.Int32);
                            if (valueExpressions[mode] is not IrExpression.Constant { Value: 4 })
                                Error(offset, "String.Equals currently requires a directly proven StringComparison.Ordinal constant; other modes and aliases require later lowering.", DiagnosticIds.ExecutionUnsupported);
                            int ordinalRight = Pop(offset, IrType.Reference(stringType!));
                            int ordinalLeft = Pop(offset, IrType.Reference(stringType!));
                            Push(offset, IrType.Int32, new IrExpression.StringEquals(ordinalLeft, ordinalRight), true); break;
                        case "call" when method.Intrinsics.TryGetValue(offset, out var emptyArray) && emptyArray.Kind == ValueIntrinsic.EmptyArray:
                            Push(offset, IrType.Reference(emptyArray.Type), new IrExpression.EmptyArray(emptyArray.Type), true); break;
                        case "call" when method.Intrinsics.TryGetValue(offset, out var stringConstruction) && stringConstruction.Kind == ValueIntrinsic.StringFromCodeUnits:
                            Push(offset, IrType.Reference(stringType!), new IrExpression.StringFromCodeUnits(
                                Pop(offset, IrType.Reference(ObjectTypeId.Vector(objectBase, ObjectArrayElement.Int32)))), true); break;
                        case "call" when method.Intrinsics.TryGetValue(offset, out var cache) && cache.Kind is ValueIntrinsic.IntegerTextFind or ValueIntrinsic.IntegerTextPublish:
                            int cacheText = cache.Kind == ValueIntrinsic.IntegerTextPublish ? Pop(offset, IrType.Reference(stringType!)) : -1;
                            int cacheIndex = Pop(offset, IrType.Int32);
                            Push(offset, IrType.Reference(stringType!), cache.Kind == ValueIntrinsic.IntegerTextFind
                                ? new IrExpression.IntegerTextFind(cacheIndex) : new IrExpression.IntegerTextPublish(cacheIndex, cacheText), true); break;
                        case "newobj" or "call" or "callvirt" when method.Intrinsics.ContainsKey(offset):
                            var intrinsic = method.Intrinsics[offset];
                            int rightText = intrinsic.Kind == ValueIntrinsic.StringCodeUnit ? Pop(offset, IrType.Int32)
                                : intrinsic.Kind is ValueIntrinsic.StringEqual or ValueIntrinsic.StringNotEqual or ValueIntrinsic.StringConcat
                                    ? Pop(offset, IrType.Reference(stringType!)) : -1;
                            int operand = intrinsic.Parameters == 0 ? -1 : Pop(offset, IrType.Reference(intrinsic.Kind == ValueIntrinsic.ExceptionMessage ? exceptions!.Base : intrinsic.Kind == ValueIntrinsic.ExceptionParameterName ? intrinsic.Type : stringType!));
                            IrExpression expression = intrinsic.Kind switch {
                                ValueIntrinsic.InvariantCulture => new IrExpression.InvariantCulture(intrinsic.Type),
                                ValueIntrinsic.NewException => new IrExpression.NewException(intrinsic.Type, operand < 0 ? null : operand),
                                ValueIntrinsic.ExceptionParameterName => new IrExpression.ExceptionParameterName(operand),
                                ValueIntrinsic.ExceptionMessage => new IrExpression.ExceptionMessage(operand),
                                ValueIntrinsic.StringLength => new IrExpression.StringLength(operand),
                                ValueIntrinsic.StringCodeUnit => new IrExpression.StringCodeUnit(operand, rightText),
                                ValueIntrinsic.StringConcat => new IrExpression.StringConcat(operand, rightText),
                                ValueIntrinsic.StringEqual or ValueIntrinsic.StringNotEqual => new IrExpression.StringEquals(operand, rightText),
                                _ => throw new InvalidOperationException("Unvalidated intrinsic kind.") };
                            if (intrinsic.Kind == ValueIntrinsic.StringNotEqual)
                            {
                                int equal = Define(offset, IrType.Int32, expression, true);
                                int falseValue = Define(offset, IrType.Int32, new IrExpression.Constant(0));
                                expression = new IrExpression.Compare(Int32Comparison.Equal, equal, falseValue);
                            }
                            Push(offset, intrinsic.Kind is ValueIntrinsic.NewException or ValueIntrinsic.InvariantCulture ? IrType.Reference(intrinsic.Type)
                                : intrinsic.Kind is ValueIntrinsic.ExceptionMessage or ValueIntrinsic.ExceptionParameterName or ValueIntrinsic.StringConcat ? IrType.Reference(stringType!) : IrType.Int32, expression, intrinsic.Kind != ValueIntrinsic.StringNotEqual); break;
                        case "newarr" when arrays:
                            var arrayType = method.ArrayOperands[offset];
                            Push(offset, IrType.Reference(arrayType), new IrExpression.NewArray(arrayType, Pop(offset, IrType.Int32)), true); break;
                        case "ldlen" when arrays:
                            if (index + 1 >= end || input.Instructions[index + 1].OpCode != "conv.i4")
                                Error(offset, "Array length requires an adjacent conv.i4 in the same block; native integer flow requires later lowering.", DiagnosticIds.ExecutionUnsupported);
                            int lengthArray = PopArray(offset); ++index;
                            Push(offset, IrType.Int32, new IrExpression.ArrayLength(lengthArray), true); break;
                        case "ldelem.ref" or "ldelem" when types.IntegerCasts:
                        case "ldelem.i4" when arrays:
                            int loadIndex = Pop(offset, IrType.Int32), loadArray = PopArray(offset);
                            var loadView = ArrayView(loadArray, opcode, offset);
                            Push(offset, loadView.ArrayElement!.Kind == ObjectArrayElementKind.Int32 ? IrType.Int32 : IrType.Reference(loadView.ArrayElement.Type!),
                                new IrExpression.LoadElement(loadView, loadArray, loadIndex), true); break;
                        case "stelem.ref" or "stelem" when types.IntegerCasts:
                        case "stelem.i4" when arrays:
                            int elementValue = Pop(offset), storeIndex = Pop(offset, IrType.Int32), storeArray = PopArray(offset);
                            var storeView = ArrayView(storeArray, opcode, offset);
                            elementValue = AdaptBox(elementValue, storeView.ArrayElement!.Kind == ObjectArrayElementKind.Int32 ? IrType.Int32 : IrType.Reference(storeView.ArrayElement.Type!), offset);
                            Compatible(values[elementValue], storeView.ArrayElement!.Kind == ObjectArrayElementKind.Int32 ? IrType.Int32 : IrType.Reference(storeView.ArrayElement.Type!), offset);
                            Add(new IrStatement.StoreElement(offset, storeView, storeArray, storeIndex, elementValue), offset); break;
                        case "ldsfld" when types.IntegerCasts && method.DefaultStatics.TryGetValue(offset, out var defaultStatic):
                            if (defaultStatic.Kind == IrTypeKind.Reference) Push(offset, IrType.NullReference, new IrExpression.Null());
                            else Push(offset, defaultStatic, defaultStatic == IrType.Int32 ? new IrExpression.Constant(0)
                                : defaultStatic == IrType.Int64 ? new IrExpression.Constant64(0) : new IrExpression.DefaultValue(defaultStatic.Class!));
                            break;
                        case "ldsfld" when method.StaticFields.TryGetValue(offset, out var readStatic):
                            Push(offset, readStatic.Type, new IrExpression.LoadStatic(readStatic.Id), true); break;
                        case "stsfld" when method.StaticFields.TryGetValue(offset, out var writeStatic):
                            Add(new IrStatement.StoreStatic(offset, writeStatic.Id, Pop(offset, writeStatic.Type)), offset); break;
                        case "ldnull": Push(offset, IrType.NullReference, new IrExpression.Null()); break;
                        case "ldarg.0" or "ldarg.1" or "ldarg.2" or "ldarg.3" or "ldarg.s" or "ldarg":
                            int argument = Variable(instruction, method.Parameters.Length);
                            int loadedArgument = Define(offset, method.Parameters[argument], new IrExpression.Parameter(argument));
                            stack.Add(Normalize(loadedArgument, ParameterStorage(argument), offset)); break;
                        case "ldloc.0" or "ldloc.1" or "ldloc.2" or "ldloc.3" or "ldloc.s" or "ldloc":
                            int local = Variable(instruction, method.Locals.Length);
                            Push(offset, method.Locals[local], new IrExpression.Local(local)); break;
                        case "ldloca" or "ldloca.s":
                            int localAddress = Variable(instruction, method.Locals.Length);
                            if (IntegerCasts.NarrowStorage(input.Locals[localAddress]) is not null)
                                Error(offset, "Narrow local addresses require width-preserving borrow storage.", DiagnosticIds.ExecutionUnsupported);
                            Push(offset, IrType.Location(method.Locals[localAddress]), new IrExpression.AddressLocal(localAddress)); break;
                        case "ldarga" or "ldarga.s":
                            int parameterAddress = Variable(instruction, method.Parameters.Length);
                            if (ParameterStorage(parameterAddress) is { } parameterStorage && IntegerCasts.NarrowStorage(parameterStorage) is not null)
                                Error(offset, "Narrow parameter addresses require width-preserving borrow storage.", DiagnosticIds.ExecutionUnsupported);
                            if (method.Parameters[parameterAddress].Kind == IrTypeKind.ManagedLocation)
                                Error(offset, "Taking the address of a borrowed parameter would create a nested byref.", DiagnosticIds.ExecutionUnsupported);
                            Push(offset, IrType.Location(method.Parameters[parameterAddress]), new IrExpression.AddressParameter(parameterAddress)); break;
                        case "starg" or "starg.s" when types.IntegerCasts:
                            int storedParameter = Variable(instruction, method.Parameters.Length);
                            if (method.Parameters[storedParameter].Kind == IrTypeKind.ManagedLocation || storedParameter == 0 && method.Binding.Kind != ObjectMethodKind.Static)
                                Error(offset, "Rebinding a borrowed parameter or an instance receiver requires later lowering.", DiagnosticIds.ExecutionUnsupported);
                            int parameterValue = Normalize(Pop(offset, method.Parameters[storedParameter]), ParameterStorage(storedParameter), offset);
                            int parameterLocation = Define(offset, IrType.Location(method.Parameters[storedParameter]), new IrExpression.AddressParameter(storedParameter));
                            Add(new IrStatement.StoreLocation(offset, parameterLocation, parameterValue), offset); break;
                        case "stloc.0" or "stloc.1" or "stloc.2" or "stloc.3" or "stloc.s" or "stloc":
                            int storedLocal = Variable(instruction, method.Locals.Length);
                            Add(new IrStatement.StoreLocal(offset, storedLocal,
                                Normalize(Pop(offset, method.Locals[storedLocal]), input.Locals[storedLocal], offset)), offset); break;
                        case "ldc.i4.m1" or "ldc.i4.0" or "ldc.i4.1" or "ldc.i4.2" or "ldc.i4.3" or "ldc.i4.4" or "ldc.i4.5" or "ldc.i4.6" or "ldc.i4.7" or "ldc.i4.8":
                            Push(offset, IrType.Int32, new IrExpression.Constant(opcode == "ldc.i4.m1" ? -1 : opcode[^1] - '0')); break;
                        case "ldc.i4.s" or "ldc.i4": Push(offset, IrType.Int32, new IrExpression.Constant(instruction.IntegerOperand!.Value)); break;
                        case "ldc.i8" when types.IntegerCasts:
                            if (instruction.Int64Operand is null) Error(offset, "ldc.i8 requires a complete Int64 operand.", DiagnosticIds.InvalidOperand);
                            Push(offset, IrType.Int64, new IrExpression.Constant64(instruction.Int64Operand!.Value)); break;
                        case "dup": int duplicate = Pop(offset); stack.Add(duplicate); stack.Add(duplicate); break;
                        case "pop": Pop(offset); break;
                        case "not":
                            int complement = Pop(offset); if (!Integer(values[complement])) Error(offset, "not requires an integer operand.");
                            Push(offset, values[complement], values[complement] == IrType.Int64 ? new IrExpression.Not64(complement)
                                : new IrExpression.Unary(Int32UnaryOperation.BitwiseNot, complement)); break;
                        case "neg":
                            int negated = Pop(offset); if (!Integer(values[negated])) Error(offset, "neg requires an integer operand.");
                            var negatedType = values[negated];
                            int zero = Define(offset, negatedType, negatedType == IrType.Int64 ? new IrExpression.Constant64(0) : new IrExpression.Constant(0));
                            Push(offset, negatedType, negatedType == IrType.Int64 ? new IrExpression.Binary64(Int64Operation.SubtractWrapping, zero, negated)
                                : new IrExpression.Binary(Int32Operation.SubtractWrapping, zero, negated)); break;
                        case "conv.i4": int unchanged = Pop(offset, IrType.Int32); stack.Add(unchanged); break;
                        case "ceq" or "clt" or "clt.un" or "cgt" or "cgt.un":
                            int comparedRight = Pop(offset), comparedLeft = Pop(offset);
                            var compared = opcode switch
                            {
                                "ceq" => Int32Comparison.Equal, "clt" => Int32Comparison.LessSigned, "clt.un" => Int32Comparison.LessUnsigned,
                                "cgt" => Int32Comparison.GreaterSigned, _ => Int32Comparison.GreaterUnsigned,
                            };
                            if (arrays && opcode == "cgt.un" && Reference(values[comparedLeft]) && values[comparedRight] == IrType.NullReference)
                                compared = Int32Comparison.NotEqual;
                            Comparison(compared, comparedLeft, comparedRight, offset);
                            Push(offset, IrType.Int32, new IrExpression.Compare(compared, comparedLeft, comparedRight)); break;
                        case "ldftn" when delegates.Count != 0:
                            if (index + 1 >= end || input.Instructions[index + 1] is not { OpCode: "newobj" } next
                                || !method.DelegateOperands.ContainsKey(next.Offset))
                                Error(offset, "ldftn requires an adjacent delegate constructor in the same block; escaping method pointers and branch entries into construction are unsupported.", DiagnosticIds.ExecutionUnsupported);
                            var construction = input.Instructions[++index];
                            var callableType = method.DelegateOperands[construction.Offset];
                            int capture = Pop(offset);
                            if (!Reference(values[capture])) Error(offset, "Delegate capture requires a reference receiver or null.");
                            Push(construction.Offset, IrType.Reference(callableType), new IrExpression.NewDelegate(callableType, method.Calls[offset], capture), true);
                            break;
                        case "callvirt" when method.DelegateOperands.ContainsKey(offset):
                            var delegateId = method.DelegateOperands[offset]; var declaration = delegates[delegateId];
                            var delegateArguments = new int[declaration.ParameterCount];
                            for (int i = delegateArguments.Length - 1; i >= 0; --i) delegateArguments[i] = Pop(offset, declaration.Parameters[i]);
                            int calledDelegate = Pop(offset, IrType.Reference(delegateId));
                            if (declaration.ReturnsVoid) Add(new IrStatement.InvokeDelegate(offset, delegateId, calledDelegate, [.. delegateArguments]), offset);
                            else Push(offset, IrType.Int32, new IrExpression.CallDelegate(delegateId, calledDelegate, [.. delegateArguments]), true);
                            break;
                        case "newobj" when method.DelegateOperands.ContainsKey(offset):
                            Error(offset, "Delegate constructor requires a preceding adjacent ldftn in the same block.", DiagnosticIds.ExecutionUnsupported); break;
                        case "callvirt" when method.InterfaceOperands.TryGetValue(offset, out var contract):
                            var interfaceArguments = new int[contract.Parameters.Length];
                            for (int i = interfaceArguments.Length - 1; i >= 0; --i) interfaceArguments[i] = Pop(offset, contract.Parameters[i]);
                            var interfaceMethod = IrMethodId.From(contract.Source);
                            if (contract.ReturnType == IrType.Void) Add(new IrStatement.Invoke(offset, interfaceMethod, [.. interfaceArguments], NullCheckReceiver: true, VirtualDispatch: true), offset);
                            else Push(offset, contract.ReturnType, new IrExpression.Call(interfaceMethod, [.. interfaceArguments], NullCheckReceiver: true, VirtualDispatch: true), true);
                            break;
                        case "newobj" when classes:
                        case "callvirt" when types.IntegerCasts:
                        case "call":
                            var target = methods[method.Calls[offset]]; int skip = opcode == "newobj" ? 1 : 0;
                            bool constrained = method.ConstrainedCalls.TryGetValue(offset, out var constrainedCall);
                            if (constrained && (target.Binding.Kind != ObjectMethodKind.ValueInstance || target.Binding.Owner != constrainedCall!.Receiver))
                                Error(offset, "Constrained call does not match its exact checked value receiver.");
                            bool nullCheck = opcode == "callvirt" && !constrained;
                            var arguments = new int[target.Parameters.Length - skip];
                            for (int i = arguments.Length - 1; i >= 0; i--)
                            {
                                if (i == 0 && method.BoxedRenderCalls.TryGetValue(offset, out var boxedReceiver))
                                {
                                    if (skip != 0 || arguments.Length != 1 || target.Binding.Kind != ObjectMethodKind.ValueInstance
                                        || target.Binding.Owner != boxedReceiver || target.Parameters[0] != IrType.Location(IrType.Value(boxedReceiver)))
                                        Error(offset, "Boxed text call requires its exact payload receiver.");
                                    int box = Pop(offset, IrType.Reference(objectBase));
                                    arguments[0] = Define(offset, IrType.Location(IrType.Value(boxedReceiver)), new IrExpression.UnboxLocation(boxedReceiver, box), true);
                                }
                                else arguments[i] = Pop(offset, target.Parameters[i + skip]);
                            }
                            if (method.BoxedFormatCalls.TryGetValue(offset, out var formatBox)
                                && (arguments.Length != 2 || valueExpressions[arguments[1]] is not IrExpression.BoxValue proof || proof.Type != formatBox))
                                Error(offset, "String.Format requires the directly proven box in the same control-flow block.", DiagnosticIds.ExecutionUnsupported);
                            if (target.Binding.InvariantProviderOnly && (arguments.Length != 2 || valueExpressions[arguments[1]] is not IrExpression.InvariantCulture))
                                Error(offset, "ToString(IFormatProvider) currently requires a directly proven CultureInfo.InvariantCulture result; null, other providers and provider aliases need later lowering.", DiagnosticIds.ExecutionUnsupported);
                            if (skip != 0 && target.Binding.Kind == ObjectMethodKind.ValueConstructor)
                            {
                                var constructed = IrType.Value(target.Binding.Owner);
                                if (!constructorTemporaries.TryGetValue(offset, out int temporary))
                                {
                                    if (locals.Count >= IrLimits.MaximumLocals) Error(offset, "Value constructor temporary local limit exceeded.", DiagnosticIds.TranslationLimit);
                                    temporary = locals.Count; locals.Add(constructed); constructorTemporaries.Add(offset, temporary);
                                }
                                int address = Define(offset, IrType.Location(constructed), new IrExpression.AddressLocal(temporary));
                                Add(new IrStatement.InitializeLocation(offset, address), offset);
                                Add(new IrStatement.Invoke(offset, target.Binding.Method, [address, .. arguments]), offset);
                                Push(offset, constructed, new IrExpression.LoadLocation(address), true);
                            }
                            else if (skip != 0) Push(offset, IrType.Reference(target.Binding.Owner), new IrExpression.NewObject(target.Binding.Method, [.. arguments]), true);
                            else if (target.ReturnType == IrType.Void) Add(new IrStatement.Invoke(offset, target.Binding.Method, [.. arguments], NullCheckReceiver: nullCheck, VirtualDispatch: nullCheck && target.Binding.VirtualSlot is not null), offset);
                            else Push(offset, target.ReturnType, new IrExpression.Call(target.Binding.Method, [.. arguments], NullCheckReceiver: nullCheck, VirtualDispatch: nullCheck && target.Binding.VirtualSlot is not null), true);
                            break;
                        case "ldfld" or "ldflda" or "stfld":
                            var field = fields[method.Fields[offset]]; var fieldType = Storage(field.Storage);
                            int fieldValue = opcode == "stfld" ? Pop(offset, fieldType) : -1;
                            int receiver = Pop(offset); var receiverType = values[receiver];
                            if (classes && types.Valid(IrType.Reference(field.Id.Owner), storage: true))
                            {
                                Compatible(receiverType, IrType.Reference(field.Id.Owner), offset);
                                if (opcode == "ldfld") Push(offset, fieldType, new IrExpression.LoadField(field.Id, receiver), true);
                                else if (opcode == "stfld")
                                {
                                    if (fieldType.Kind == IrTypeKind.Value && valueExpressions[fieldValue] is IrExpression.Call)
                                        Error(offset, "Direct struct-return calls stored into heap fields need destination/exception-order lowering; materialized copies and explicit managed locations are supported.", DiagnosticIds.ExecutionUnsupported);
                                    Add(new IrStatement.StoreField(offset, field.Id, receiver, fieldValue), offset);
                                }
                                else Push(offset, IrType.Location(fieldType, (field.Attributes & FieldAttributes.InitOnly) != 0), new IrExpression.AddressField(field.Id, receiver), true);
                            }
                            else if (opcode == "ldfld" && receiverType == IrType.Value(field.Id.Owner))
                                Push(offset, fieldType, new IrExpression.ValueField(field.Id, receiver));
                            else
                            {
                                if (receiverType.Kind != IrTypeKind.ManagedLocation || receiverType.Element != IrType.Value(field.Id.Owner))
                                    Error(offset, "Value field operation requires the exact inline receiver or its managed location.");
                                bool initializes = method.Binding.Kind == ObjectMethodKind.ValueConstructor && method.Binding.Owner == field.Id.Owner
                                    && valueExpressions[receiver] is IrExpression.Parameter { Index: 0 };
                                var locationType = IrType.Location(fieldType, receiverType.ReadOnly || !initializes && (field.Attributes & FieldAttributes.InitOnly) != 0);
                                int address = Define(offset, locationType, new IrExpression.LocationField(field.Id, receiver));
                                if (opcode == "ldflda") stack.Add(address);
                                else if (opcode == "ldfld") Push(offset, fieldType, new IrExpression.LoadLocation(address), true);
                                else { Writable(address, offset); Add(new IrStatement.StoreLocation(offset, address, fieldValue), offset); }
                            }
                            break;
                        case "box" or "unbox.any" when boxing && method.ReferenceBoxOperands.TryGetValue(offset, out var referenceBox):
                            int referenceOperand = Pop(offset, opcode == "box" ? referenceBox : null);
                            if (!Reference(values[referenceOperand])) Error(offset, "Reference boxing/casting requires an object reference or null.");
                            if (opcode == "box") stack.Add(referenceOperand);
                            else Push(offset, referenceBox, new IrExpression.CastClass(referenceBox.Class!, referenceOperand), true);
                            break;
                        case "box" or "unbox" or "unbox.any" when boxing:
                            var boxedType = method.TypeOperands[offset];
                            int boxedOperand = opcode == "box" ? Pop(offset, IrType.Value(boxedType)) : Pop(offset);
                            if (opcode != "box" && !Reference(values[boxedOperand])) Error(offset, "Unboxing requires an object reference or null.");
                            if (opcode == "box") Push(offset, IrType.Reference(objectBase), new IrExpression.BoxValue(boxedType, boxedOperand), true);
                            else if (opcode == "unbox") Push(offset, IrType.Location(IrType.Value(boxedType)), new IrExpression.UnboxLocation(boxedType, boxedOperand), true);
                            else Push(offset, IrType.Value(boxedType), new IrExpression.UnboxValue(boxedType, boxedOperand), true);
                            break;
                        case "initobj" or "ldobj" or "stobj" or "cpobj":
                            var valueType = method.MemoryOperands.TryGetValue(offset, out var memoryType) ? memoryType : IrType.Value(method.TypeOperands[offset]);
                            int stored = opcode == "stobj" ? Pop(offset, valueType) : -1;
                            if (opcode == "cpobj")
                            {
                                int source = Location(offset, valueType);
                                // Snapshot every leaf before touching a possibly overlapping destination.
                                stored = Define(offset, valueType, new IrExpression.LoadLocation(source), true);
                            }
                            int destination = Location(offset, valueType);
                            if (opcode == "ldobj") Push(offset, valueType, new IrExpression.LoadLocation(destination), true);
                            else
                            {
                                Writable(destination, offset);
                                Add(opcode == "initobj" ? new IrStatement.InitializeLocation(offset, destination)
                                    : new IrStatement.StoreLocation(offset, destination, stored), offset);
                            }
                            break;
                        case "ldind.i8" or "ldind.u4" when types.IntegerCasts:
                        case "ldind.i4" or "ldind.ref":
                            int read = Location(offset, opcode == "ldind.i8" ? IrType.Int64 : opcode is "ldind.i4" or "ldind.u4" ? IrType.Int32 : null);
                            if (opcode == "ldind.ref" && values[read].Element!.Kind != IrTypeKind.Reference) Error(offset, "ldind.ref requires reference storage.");
                            Push(offset, values[read].Element!, new IrExpression.LoadLocation(read), true); break;
                        case "stind.i8" when types.IntegerCasts:
                        case "stind.i4" or "stind.ref":
                            int writeValue = Pop(offset), write = Location(offset, opcode == "stind.i8" ? IrType.Int64 : opcode == "stind.i4" ? IrType.Int32 : null);
                            if (opcode == "stind.ref" && values[write].Element!.Kind != IrTypeKind.Reference) Error(offset, "stind.ref requires reference storage.");
                            Compatible(values[writeValue], values[write].Element!, offset); Writable(write, offset);
                            Add(new IrStatement.StoreLocation(offset, write, writeValue), offset); break;
                        case "br" or "br.s": terminator = new IrJump(offset, Edge(instruction.Targets[0], offset)); break;
                        case "ret":
                            int returned = method.ReturnType == IrType.Void ? -1 : Pop(offset, method.ReturnType);
                            if (stack.Count != 0) Error(offset, "Return leaves residual evaluation-stack values.");
                            terminator = returned < 0 ? new IrVoidReturn(offset) : new IrReturn(offset, Normalize(returned, input.Signature.ReturnType, offset)); break;
                        default:
                            if (CilControlFlow.BranchComparison(opcode) is { } comparison)
                            {
                                int? right = comparison is Int32Comparison.Zero or Int32Comparison.Nonzero ? null : Pop(offset);
                                int left = Pop(offset);
                                if (right is { } comparedRightId) Comparison(comparison, left, comparedRightId, offset);
                                else if (!Integer(values[left]) && !(arrays && Reference(values[left]))) Error(offset, "Branch requires integer or reference truth value.");
                                terminator = new IrBranch(offset, comparison, left, right, Edge(instruction.Targets[0], offset), Edge(offset + instruction.Length, offset));
                            }
                            else Error(offset, "Instruction '" + opcode + "' is outside " + (boxing ? BoxedProfile : classes ? ObjectProfile : arrays ? ArrayProfile : Profile) + "; add a supported lowering before execution.", DiagnosticIds.ExecutionUnsupported);
                            break;
                    }
                    if (stack.Count > input.MaxStack) Error(offset, "Evaluation stack exceeds the declared maximum.");
                }
                var last = input.Instructions[end - 1];
                terminator ??= new IrJump(last.Offset, Edge(last.Offset + last.Length, last.Offset));
                return new(id, blockOffset, [.. incoming[id].Where((_, index) => !incomingThis[id][index])], [.. statements], terminator);

                void Add(IrStatement statement, int offset)
                {
                    if (++_statements > IrLimits.MaximumStatements) Error(offset, "Aggregate IR statement limit exceeded.", DiagnosticIds.TranslationLimit);
                    statements.Add(statement);
                }
                int Define(int offset, IrType type, IrExpression expression, bool fault = false)
                {
                    int id = values.Count; values.Add(type); valueExpressions.Add(expression);
                    Add(new IrStatement.Define(new(id, type, offset, expression), fault ? IrExceptionTarget.MethodExit : null), offset); return id;
                }
                void Push(int offset, IrType type, IrExpression expression, bool fault = false) => stack.Add(Define(offset, type, expression, fault));
                TypeSignature? ParameterStorage(int parameter)
                {
                    int index = parameter - ((input.Signature.Header & 0x20) != 0 ? 1 : 0);
                    return index < 0 ? null : input.Signature.Parameters[index];
                }
                int Normalize(int operand, TypeSignature? signature, int offset) => types.IntegerCasts && signature is not null
                    && IntegerCasts.NarrowStorage(signature) is { } target
                    ? Define(offset, IrType.Int32, new IrExpression.IntegerCast(target, operand, false, false)) : operand;
                int Pop(int offset, IrType? expected = null)
                {
                    if (stack.Count == 0) Error(offset, "Instruction requires an evaluation-stack operand.");
                    int id = stack[^1]; stack.RemoveAt(stack.Count - 1);
                    if (expected is not null) { id = AdaptBox(id, expected, offset); Compatible(values[id], expected, offset); } return id;
                }
                int AdaptBox(int id, IrType expected, int offset)
                {
                    if (expected.Kind == IrTypeKind.Reference && !types.Assignable(values[id], expected)
                        && valueExpressions[id] is IrExpression.BoxValue box && boxedRelations?.Accepts(box.Type, expected.Class!) == true)
                        return Define(offset, expected, new IrExpression.CastClass(expected.Class!, id), true);
                    return id;
                }
                int Location(int offset, IrType? element)
                {
                    int id = Pop(offset);
                    if (values[id].Kind != IrTypeKind.ManagedLocation || element is not null && values[id].Element != element)
                        Error(offset, "Instruction requires a managed location with the exact element type.");
                    return id;
                }
                void Writable(int id, int offset)
                {
                    if (values[id].ReadOnly) Error(offset, "Readonly managed storage cannot be mutated.");
                }
                int PopArray(int offset)
                {
                    int array = Pop(offset);
                    if (types.IntegerCasts)
                    {
                        if (values[array] != IrType.NullReference && values[array].Class?.ArrayElement is null)
                            Error(offset, "Array operation requires a known vector or null.");
                    }
                    else if (integerArray is null || values[array] != IrType.NullReference && values[array] != IrType.Reference(integerArray))
                        Error(offset, "Array operation requires an Int32 vector or null.");
                    return array;
                }
                ObjectTypeId ArrayView(int receiver, string opcode, int offset)
                {
                    if (method.ArrayOperands.TryGetValue(offset, out var declared))
                    {
                        Compatible(values[receiver], IrType.Reference(declared), offset); return declared;
                    }
                    bool reference = opcode.EndsWith(".ref", StringComparison.Ordinal);
                    var view = values[receiver] == IrType.NullReference
                        ? reference ? ObjectTypeId.Vector(objectBase, ObjectArrayElement.Reference(objectBase)) : integerArray!
                        : values[receiver].Class!;
                    if (view.ArrayElement!.Kind != (reference ? ObjectArrayElementKind.Reference : ObjectArrayElementKind.Int32))
                        Error(offset, "Array element opcode does not match its storage width/kind.");
                    return view;
                }
                void Comparison(Int32Comparison comparison, int left, int right, int offset)
                {
                    if (Integer(values[left]) && values[right] == values[left]) return;
                    if (arrays && comparison is (Int32Comparison.Equal or Int32Comparison.NotEqual) && Reference(values[left]) && Reference(values[right])) return;
                    Error(offset, "Comparison requires matching admitted integer widths or supported reference identity equality.");
                }
                IrEdge Edge(int offset, int from)
                {
                    if (!partition.BlockAt.TryGetValue(offset, out int target)) Error(from, "Control transfer falls outside this method.", DiagnosticIds.InvalidControlTransfer);
                    var stackTypes = stack.Select(id => values[id]).ToImmutableArray();
                    var receiverSlots = stack.Select(value => method.Binding.Kind is (ObjectMethodKind.ValueConstructor or ObjectMethodKind.ValueInstance or ObjectMethodKind.PrimitiveInstance)
                        && valueExpressions[value] is IrExpression.Parameter { Index: 0 }).ToImmutableArray();
                    if (stackTypes.Where((type, index) => type.Kind == IrTypeKind.ManagedLocation && !receiverSlots[index]).Any())
                        Error(from, "Managed locations other than the direct value receiver cannot cross block joins in this profile.", DiagnosticIds.ExecutionUnsupported);
                    if (!incoming[target].IsDefault && !incoming[target].SequenceEqual(stackTypes))
                    {
                        if (exceptions is null || incoming[target].Length != stackTypes.Length) Error(from, "Block receives incompatible stack types or depths.");
                        var joined = incoming[target].Select((prior, index) => types.Join(prior, stackTypes[index])).ToArray();
                        if (joined.Any(t => t is null)) Error(from, "Reference join has no unique most-specific common type; use a typed local or explicit cast.");
                        var widened = joined.Select(t => t!).ToImmutableArray();
                        if (!incoming[target].SequenceEqual(widened)) { incoming[target] = widened; pending.Enqueue(target); }
                    }
                    if (incoming[target].IsDefault) { incoming[target] = stackTypes; incomingThis[target] = receiverSlots; pending.Enqueue(target); }
                    else if (!incomingThis[target].SequenceEqual(receiverSlots)) Error(from, "Constructor receiver provenance differs across incoming edges.");
                    _edges += stackTypes.Length;
                    if (_edges > IrLimits.MaximumEdgeArguments) Error(from, "Aggregate edge-argument limit exceeded.", DiagnosticIds.TranslationLimit);
                    return new(target, [.. stack.Where((_, index) => !receiverSlots[index])]);
                }
            }
        }
        private void Compatible(IrType actual, IrType expected, int offset)
        {
            if (classes && types.Assignable(actual, expected)) return;
            if (actual != expected && !(actual == IrType.NullReference && expected.Kind == IrTypeKind.Reference))
                Error(offset, "Stack operand does not match its required exact storage type.");
        }
        private static bool Reference(IrType type) => type.Kind is IrTypeKind.Reference or IrTypeKind.NullReference;
        private int Variable(MethodInstruction instruction, int count)
        {
            if (instruction.IntegerOperand is not int index || index < 0 || index >= count)
                Error(instruction.Offset, "Variable index is outside the method signature.", DiagnosticIds.InvalidOperand);
            return instruction.IntegerOperand!.Value;
        }
        private static IrType Storage(ObjectStorage storage) => storage switch
        {
            ObjectStorage.Int32 => IrType.Int32,
            ObjectStorage.Int64 => IrType.Int64,
            ObjectStorage.Value value => IrType.Value(value.Type),
            ObjectStorage.Reference reference => IrType.Reference(reference.Type),
            _ => throw new InvalidOperationException("Unknown validated storage."),
        };
        private bool Integer(IrType type) => type == IrType.Int32 || types.IntegerCasts && type == IrType.Int64;
    }
}
