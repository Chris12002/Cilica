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
    public ImmutableArray<ObjectAbstractMethodDeclaration> AbstractMethods { get; init; } = [];
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
    public const string IntegerObjectProfile = "integer-managed-objects-cil-v126";
    public static ManagedExceptionTranslationResult TranslateIntegerObjects(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits = null)
    {
        var result = TranslateCore(resolver, assembly, entry, runtimeLibrary, limits, arrays: true, classes: true, boxing: true, generics: true, delegates: true, exceptions: true, integers64: true);
        return new(result.Program is null ? null : new(new(result.Program, result.Bindings) { InterfaceDispatch = result.InterfaceDispatch, Dispatch = result.Dispatch, AbstractMethods = result.AbstractMethods }, result.Exceptions!), result.Diagnostics);
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
            var referenceValues = input.Methods.Any(m => m.KeyOperations.Values.Any(k => k.Operation == ClosedKeyOperation.ContainsReferences))
                ? ValueLayoutPlanner.Build(input.Types).Where(p => p.Slots.Any(s => s.Storage is ObjectStorage.Reference)).Select(p => p.Type).ToHashSet()
                : new HashSet<ObjectTypeId>();
            var resources = new ProgramResources(); var translated = new List<IrMethod>();
            foreach (var method in input.Methods)
            {
                var body = new Body(method, methods, fields, arrays, classes, boxing, input.Types.References.Delegates.ToDictionary(d => ObjectTypeId.From(d.Source)), types, input.Types.References.ObjectBase,
                    input.Types.References.Arrays.FirstOrDefault(a => a.ArrayElement!.Kind == ObjectArrayElementKind.Int32), input.Exceptions, input.Types.References.String, boxedRelations, referenceValues, input.Types.Types.FirstOrDefault(t => t.Source.Name == "System.Int32" && t.Source.ContentHash == input.Types.References.ObjectBase.ContentHash && t.ScalarBox == PrimitiveTypeCode.Int32) is { } intLayout ? ObjectTypeId.From(intLayout.Source) : null).Translate();
                if (!resources.Add(body)) Fail(method, 0, "Aggregate value IR resource limit exceeded.", DiagnosticIds.TranslationLimit);
                translated.Add(body);
            }
            var program = new ManagedLocationIrProgram(new(input.Entry, [.. translated], input.Types));
            if (integers64) program = RetainUsedVectors(program);
            var interfacePlan = input.InterfaceDispatch is null ? null : input.InterfaceDispatch with { Types = program.Flow.Types.References, Values = input.InterfaceDispatch.Values is null ? null : program.Flow.Types };
            if (interfacePlan is not null)
            {
                var arraysKept = program.Flow.Types.References.Arrays.ToHashSet();
                var maps = interfacePlan.Dispatch.Select(map => map with {
                    Targets = [.. map.Targets.Where(t => t.Receiver.ArrayElement is null || arraysKept.Contains(t.Receiver))] }).ToImmutableArray();
                var retainedHelpers = maps.SelectMany(m => m.Targets).Select(t => t.Implementation).ToHashSet();
                interfacePlan = interfacePlan with { Dispatch = maps,
                    Methods = [.. interfacePlan.Methods.Where(m => !m.ArrayHelper || retainedHelpers.Contains(m.Binding.Method))] };
            }
            ImmutableArray<ObjectMethodBinding> bindings = [.. input.Methods.Select(m => m.Binding), .. input.AbstractBindings];
            try
            {
                if (exceptions) _ = ValueRootAssignment.Assign(new ManagedExceptionIrProgram(new(program, bindings) { InterfaceDispatch = interfacePlan, Dispatch = input.Dispatch, AbstractMethods = input.AbstractMethods }, input.Exceptions!));
                else if (delegates) _ = ValueRootAssignment.Assign(new DelegateValueIrProgram(program, bindings));
                else if (boxing) _ = ValueRootAssignment.Assign(new BoxedValueIrProgram(program, bindings));
                else if (classes) _ = ValueRootAssignment.Assign(new ValueObjectIrProgram(program, bindings));
                else if (arrays) _ = ValueRootAssignment.Assign(new ValueArrayIrProgram(program, bindings));
                else _ = ValueRootAssignment.Assign(program);
            }
            catch (ArgumentException error) { Fail(methods[input.Entry], 0, error.Message); }
            return new(program, bindings, []) { Exceptions = input.Exceptions, InterfaceDispatch = interfacePlan, Dispatch = input.Dispatch, AbstractMethods = input.AbstractMethods };
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
                    case IrStatement.Define { Value.Expression: IrExpression.RuntimeTypeLiteral literal }: Include(literal.Target); break;
                    case IrStatement.Define { Value.Expression: IrExpression.AddressElement address }: Include(address.ArrayType); break;
                    case IrStatement.Define { Value.Expression: IrExpression.LoadElement load }: Include(load.ArrayType); break;
                    case IrStatement.StoreElement store: Include(store.ArrayType); break;
                    case IrStatement.Define { Value.Expression: IrExpression.ArrayCopyCompatible copy }: Include(copy.ArrayType); break;
                }
        }
        foreach (var field in flow.Types.Types.SelectMany(t => t.Fields).Concat(flow.Types.References.Types.SelectMany(t => t.Fields)))
            if (field.Storage is ObjectStorage.Reference reference) Include(reference.Type);
        foreach (var argument in flow.Types.References.Interfaces.SelectMany(i => i.Arguments)) Include(argument.Reference);
        foreach (var field in flow.Types.References.Statics) Include(field.Type.Class);
        foreach (var declaration in flow.Types.References.Delegates) foreach (var type in declaration.Parameters.Append(declaration.ReturnType)) Include(type.Class);
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
        Dictionary<ObjectFieldId, ObjectField> fields, bool arrays, bool classes, bool boxing, Dictionary<ObjectTypeId, ObjectDelegateType> delegates, IrTypeRules types, ObjectTypeId objectBase, ObjectTypeId? integerArray, ManagedExceptionTypes? exceptions, ObjectTypeId? stringType, BoxedTypeRelations? boxedRelations, HashSet<ObjectTypeId> referenceValues, ObjectTypeId? actualInteger)
    {
        private int _statements, _inputs, _edges;
        private void Error(int offset, string message, string code = DiagnosticIds.InconsistentStack) => Fail(method, offset, message, code);
        internal IrMethod Translate()
        {
            var input = method.Input;
            var locals = method.Locals.ToBuilder();
            var constructorTemporaries = new Dictionary<int, int>();
            var returnedFields = new HashSet<(int Block, int Receiver, int Offset)>();
            var constrainedPrefixes = method.ConstrainedCalls.Values.Concat(method.EnumCompareCalls.Values).Concat(method.EnumTextCalls.Values).Select(c => c.PrefixOffset)
                .Concat(method.ConstrainedReferenceCalls.Values.Select(c => c.PrefixOffset)).ToHashSet();
            // Re-reading a carried class receiver is valid only while its argument
            // slot cannot be changed through a managed address. Direct starg.0 is
            // separately rejected for every instance method.
            bool stableReferenceReceiver = types.IntegerCasts && method.Binding.Kind is (ObjectMethodKind.Constructor or ObjectMethodKind.Instance)
                && !input.Instructions.Any(i => i.OpCode is "ldarga" or "ldarga.s" && i.IntegerOperand == 0);
            bool stableConstructorReceiver = stableReferenceReceiver && method.Binding.Kind == ObjectMethodKind.Constructor;
            if (input.Instructions.IsEmpty) Error(0, "Method body is empty.");
            var boundaries = method.ExceptionRegions.SelectMany(r => new[] { r.TryStart, r.TryEnd, r.HandlerStart, r.HandlerEnd, r.FilterStart ?? r.HandlerStart });
            var partition = CilControlFlow.Partition(input, (code, message, offset) => Error(offset, message, code), boundaries, exceptions is not null, switches: types.IntegerCasts);
            int BlockAt(int offset) => offset == input.CodeSize ? partition.Ranges.Count : partition.BlockAt[offset];
            var regions = method.ExceptionRegions.Select(r => r with { TryStart = BlockAt(r.TryStart), TryEnd = BlockAt(r.TryEnd),
                HandlerStart = BlockAt(r.HandlerStart), HandlerEnd = BlockAt(r.HandlerEnd), FilterStart = r.FilterStart is int first ? BlockAt(first) : null }).ToImmutableArray();
            var incoming = new ImmutableArray<IrType>[partition.Ranges.Count];
            var incomingSources = new ImmutableArray<IrExpression?>[partition.Ranges.Count];
            var blocks = new IrBlock[partition.Ranges.Count]; var pending = new Queue<int>();
            incoming[0] = []; incomingSources[0] = []; pending.Enqueue(0);
            foreach (var region in regions)
            {
                Seed(region.HandlerStart, region.Kind == IrExceptionClause.Catch ? [IrType.Reference(region.CatchType!)]
                    : region.Kind == IrExceptionClause.Filter ? [IrType.Reference(exceptions!.Base)] : []);
                if (region.FilterStart is int filter) Seed(filter, [IrType.Reference(exceptions!.Base)]);
            }
            void Seed(int id, ImmutableArray<IrType> types)
            {
                if (!incoming[id].IsDefault) { if (!incoming[id].SequenceEqual(types)) Error(0, "Conflicting exception entry stack."); return; }
                incoming[id] = types; incomingSources[id] = [.. types.Select(_ => (IrExpression?)null)]; pending.Enqueue(id);
            }
            while (pending.TryDequeue(out int id)) blocks[id] = Block(id);
            for (int id = 0; id < blocks.Length; id++)
                if (blocks[id] is null) Error(input.Instructions[partition.Ranges[id].Start].Offset,
                    "Unreachable blocks require later analysis; remove dead IL before value translation.", DiagnosticIds.ExecutionUnsupported);
            var translated = new IrMethod(input.Source, method.Parameters, method.ReturnType, [.. blocks], locals.ToImmutable()) { ExceptionRegions = regions, GenericArrays = method.GenericArrays, GenericTypeTests = method.GenericTypeTests };
            if (returnedFields.Count != 0)
                NonNullFieldReturnProof.Validate(translated, stableConstructorReceiver, stableReferenceReceiver, returnedFields,
                    (offset, message, limit) => Error(offset, message, limit ? DiagnosticIds.TranslationLimit : DiagnosticIds.ExecutionUnsupported));
            return translated;

            IrBlock Block(int id)
            {
                returnedFields.RemoveWhere(item => item.Block == id);
                var (start, end) = partition.Ranges[id]; int blockOffset = input.Instructions[start].Offset;
                int switchCase = partition.SwitchCases.GetValueOrDefault(id);
                if (switchCase > 0) blockOffset += 5 + 4 * (switchCase - 1);
                _inputs += incoming[id].Length;
                if (_inputs > IrLimits.MaximumInputs) Error(blockOffset, "Aggregate block-input limit exceeded.", DiagnosticIds.TranslationLimit);
                var statements = new List<IrStatement>(); var values = new List<IrType>(); var valueExpressions = new List<IrExpression>(); var stack = new List<int>(); var nativeIndices = new HashSet<int>();
                var nullableBoxes = new Dictionary<int, ObjectTypeId>();
                IrTerminator? terminator = null;
                int transferred = 0;
                for (int i = 0; i < incoming[id].Length; i++)
                    Push(blockOffset, incoming[id][i], incomingSources[id][i] ?? new IrExpression.StackInput(transferred++));
                for (int index = start; index < end; index++)
                {
                    var instruction = input.Instructions[index]; int offset = switchCase > 0 ? blockOffset : instruction.Offset; string opcode = instruction.OpCode;
                    if (opcode == "callvirt" && method.ConstrainedReferenceCalls.TryGetValue(offset, out var referenceCall))
                    {
                        int receiverIndex = stack.Count - referenceCall.ParameterCount - 1;
                        if (receiverIndex < 0) Error(offset, "Constrained reference call has an incomplete argument stack.");
                        int location = stack[receiverIndex];
                        var receiverType = IrType.Reference(referenceCall.Receiver);
                        if (values[location] != IrType.Location(receiverType))
                            Error(offset, "Constrained reference call requires the exact managed receiver location.");
                        // Arguments have already been evaluated. Read the current
                        // reference now; normal call lowering owns null/dispatch checks.
                        stack[receiverIndex] = Define(offset, receiverType, new IrExpression.LoadLocation(location), true);
                    }
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
                    else if (types.IntegerCasts && opcode is ("conv.ovf.i" or "conv.ovf.i.un"))
                    {
                        int operand = Pop(offset);
                        if (!Integer(values[operand])) Error(offset, "Checked native index conversion requires Int32 or Int64.");
                        int nativeIndex = Define(offset, IrType.Int32, new IrExpression.CheckedNativeIndex(operand, opcode.EndsWith(".un", StringComparison.Ordinal)), true);
                        nativeIndices.Add(nativeIndex); stack.Add(nativeIndex);
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
                        case "ldtoken" when method.EnumValues.Values.Any(e => e.TokenOffset == offset): break;
                        case "call" when method.EnumValues.Values.Any(e => e.HandleOffset == offset): break;
                        case "call" when method.EnumValues.TryGetValue(offset, out var enumValues):
                            int enumLength = Define(offset, IrType.Int32, new IrExpression.Constant(enumValues.Values.Length));
                            int enumArray = Define(offset, IrType.Reference(enumValues.ArrayType), new IrExpression.NewArray(enumValues.ArrayType, enumLength), true);
                            for (int element = 0; element < enumValues.Values.Length; ++element)
                            {
                                int enumIndex = Define(offset, IrType.Int32, new IrExpression.Constant(element));
                                int value = Define(offset, IrType.Int32, new IrExpression.Constant(enumValues.Values[element]));
                                Add(new IrStatement.StoreElement(offset, enumValues.ArrayType, enumArray, enumIndex, value), offset);
                            }
                            stack.Add(enumArray); break;
                        case "ldtoken" when method.StaticArrays.Values.Any(a => a.TokenOffset == offset): break;
                        case "call" when method.StaticArrays.TryGetValue(offset, out var staticArray):
                            int initializedArray = Pop(offset, IrType.Reference(staticArray.ArrayType));
                            for (int element = 0; element < staticArray.Values.Length; ++element)
                            {
                                int constantIndex = Define(offset, IrType.Int32, new IrExpression.Constant(element));
                                bool wide = staticArray.ArrayType.ArrayElement!.Kind == ObjectArrayElementKind.Int64;
                                int constantValue = Define(offset, wide ? IrType.Int64 : IrType.Int32,
                                    wide ? new IrExpression.Constant64(staticArray.Values[element]) : new IrExpression.Constant(unchecked((int)staticArray.Values[element])));
                                Add(new IrStatement.StoreElement(offset, staticArray.ArrayType, initializedArray, constantIndex, constantValue), offset);
                            }
                            break;
                        case "constrained." when types.IntegerCasts && constrainedPrefixes.Contains(offset): break;
                        case "ldsfld" when exceptions is not null && method.Literals.ContainsKey(offset):
                        case "call" when exceptions is not null && method.Literals.ContainsKey(offset):
                        case "ldstr" when exceptions is not null:
                            Push(offset, IrType.Reference(stringType!), new IrExpression.StringLiteral(method.Literals[offset]), true); break;
                        case "callvirt" when method.EnumTextCalls.ContainsKey(offset):
                            var enumTextTarget = methods[method.Calls[offset]];
                            if (enumTextTarget.Binding.Kind != ObjectMethodKind.Static || enumTextTarget.Parameters.Length != 1
                                || enumTextTarget.Parameters[0] != IrType.Int32 || enumTextTarget.ReturnType != IrType.Reference(stringType!))
                                Error(offset, "Constrained enum text requires its checked static Int32/string bridge.");
                            int textAddress = Pop(offset, IrType.Location(IrType.Int32));
                            int textBits = Define(offset, IrType.Int32, new IrExpression.LoadLocation(textAddress), true);
                            Push(offset, IrType.Reference(stringType!), new IrExpression.Call(enumTextTarget.Binding.Method, [textBits]), true);
                            break;
                        case "callvirt" when method.EnumCompareCalls.TryGetValue(offset, out var enumComparison):
                            int enumTarget = Pop(offset, IrType.Reference(objectBase));
                            bool nullEnumTarget = valueExpressions[enumTarget] is IrExpression.Null;
                            if (!nullEnumTarget && (valueExpressions[enumTarget] is not IrExpression.BoxValue enumBox || enumBox.Type != enumComparison.Receiver))
                                Error(offset, "Enum.CompareTo currently requires a directly boxed matching Int32 enum or null in the same block; dynamic targets and aliases require later lowering.", DiagnosticIds.ExecutionUnsupported);
                            int enumAddress = Pop(offset, IrType.Location(IrType.Int32));
                            int enumLeft = Define(offset, IrType.Int32, new IrExpression.LoadLocation(enumAddress), true);
                            if (nullEnumTarget) Push(offset, IrType.Int32, new IrExpression.Constant(1));
                            else
                            {
                                int enumRight = Define(offset, IrType.Int32, new IrExpression.UnboxValue(enumComparison.Receiver, enumTarget), true);
                                int greater = Define(offset, IrType.Int32, new IrExpression.Compare(Int32Comparison.GreaterSigned, enumLeft, enumRight));
                                int less = Define(offset, IrType.Int32, new IrExpression.Compare(Int32Comparison.LessSigned, enumLeft, enumRight));
                                Push(offset, IrType.Int32, new IrExpression.Binary(Int32Operation.SubtractWrapping, greater, less));
                            }
                            break;
                        case "call" when method.Intrinsics.TryGetValue(offset, out var tupleShape) && tupleShape.Kind == ValueIntrinsic.ClosedTupleShape:
                            Push(offset, IrType.Int32, new IrExpression.Constant(tupleShape.ConstantValue)); break;
                        case "call" when method.Intrinsics.TryGetValue(offset, out var enumText) && enumText.Kind is ValueIntrinsic.EnumBits or ValueIntrinsic.EnumCount or ValueIntrinsic.EnumNameAt or ValueIntrinsic.EnumValueAt:
                            if (enumText.Kind == ValueIntrinsic.EnumCount) Push(offset, IrType.Int32, new IrExpression.Constant(enumText.ConstantValue));
                            else if (enumText.Kind == ValueIntrinsic.EnumBits) Push(offset, IrType.Int32,
                                new IrExpression.UnboxValue(enumText.Type, Pop(offset, IrType.Reference(objectBase))), true);
                            else Push(offset, enumText.Kind == ValueIntrinsic.EnumNameAt ? IrType.Reference(stringType!) : IrType.Int32,
                                new IrExpression.EnumEntry(enumText.Type, Pop(offset, IrType.Int32), enumText.Kind == ValueIntrinsic.EnumNameAt), true);
                            break;
                        case "call" when method.Intrinsics.TryGetValue(offset, out var enumQuery) && enumQuery.Kind is ValueIntrinsic.EnumQuery or ValueIntrinsic.EnumErrorMessage:
                            int enumValue = Pop(offset, IrType.Reference(objectBase)), enumType = Pop(offset, IrType.Reference(enumQuery.RuntimeTypeBase!));
                            bool enumMessage = enumQuery.Kind == ValueIntrinsic.EnumErrorMessage;
                            Push(offset, enumMessage ? IrType.Reference(stringType!) : IrType.Int32,
                                new IrExpression.EnumQuery(enumType, enumValue, enumQuery.Type, enumMessage), true); break;
                        case "call" or "callvirt" when method.Intrinsics.TryGetValue(offset, out var clone) && clone.Kind == ValueIntrinsic.ObjectClone:
                            Push(offset, IrType.Reference(objectBase), new IrExpression.ObjectClone(clone.Type, Pop(offset, IrType.Reference(clone.Type))), true); break;
                        case "call" when method.Intrinsics.TryGetValue(offset, out var objectHash) && objectHash.Kind == ValueIntrinsic.ObjectIdentityHash:
                            Push(offset, IrType.Int32, new IrExpression.ObjectIdentityHash(Pop(offset, IrType.Reference(objectBase))), true); break;
                        case "ldtoken" when method.RuntimeTypeTokens.TryGetValue(offset, out var tokenType):
                            int typeObject = Define(offset, IrType.Reference(tokenType.Type), new IrExpression.RuntimeTypeLiteral(tokenType.Target, tokenType.Implementation), true);
                            int emptyHandle = Define(offset, IrType.Value(tokenType.Handle), new IrExpression.DefaultValue(tokenType.Handle));
                            Push(offset, IrType.Value(tokenType.Handle), new IrExpression.WithValueField(tokenType.HandleField, emptyHandle, typeObject)); break;
                        case "call" when method.Intrinsics.TryGetValue(offset, out var defaultText) && defaultText.Kind == ValueIntrinsic.ObjectDefaultText:
                            int textReceiver = Pop(offset); Push(offset, IrType.Reference(defaultText.Type), new IrExpression.ObjectDefaultText(textReceiver), true); break;
                        case "call" or "callvirt" when method.Intrinsics.TryGetValue(offset, out var runtimeType) && runtimeType.Kind is ValueIntrinsic.ObjectGetType or ValueIntrinsic.RuntimeTypeHash:
                            int typeReceiver = Pop(offset);
                            if (!Reference(values[typeReceiver])) Error(offset, "Runtime type identity requires a reference operand.");
                            Push(offset, runtimeType.Kind == ValueIntrinsic.ObjectGetType ? IrType.Reference(runtimeType.RuntimeTypeBase!) : IrType.Int32,
                                runtimeType.Kind == ValueIntrinsic.ObjectGetType ? new IrExpression.ObjectGetType(typeReceiver, runtimeType.Type)
                                    : new IrExpression.RuntimeTypeHash(typeReceiver, runtimeType.Type), true); break;
                        case "isinst" or "castclass" when exceptions is not null:
                            int tested = Pop(offset); var testedType = method.TypeOperands[offset];
                            if (!Reference(values[tested])) Error(offset, "Type tests require a reference operand.");
                            bool boxedTest = method.BoxedTypeTestOperands.Contains(offset);
                            Push(offset, IrType.Reference(boxedTest ? objectBase : testedType), opcode == "isinst"
                                ? new IrExpression.IsInstance(testedType, tested) { BoxedTarget = boxedTest }
                                : new IrExpression.CastClass(testedType, tested) { BoxedTarget = boxedTest }, true); break;
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
                            if (keyOperation.Operation == ClosedKeyOperation.ContainsReferences)
                                Push(offset, IrType.Int32, new IrExpression.Constant(keyOperation.Storage.Kind == IrTypeKind.Reference
                                    || keyOperation.Storage.Kind == IrTypeKind.Value && referenceValues.Contains(keyOperation.Storage.Class!) ? 1 : 0));
                            else if (keyOperation.Operation == ClosedKeyOperation.IsValueType)
                                Push(offset, IrType.Int32, new IrExpression.Constant(keyOperation.Storage.Kind == IrTypeKind.Reference ? 0 : 1));
                            else if (keyOperation.Operation == ClosedKeyOperation.IsNull)
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
                            if (constructorIntrinsic.ExceptionMessage is { } exceptionConstruction)
                            {
                                int shell = Define(offset, IrType.Reference(constructorIntrinsic.Type), new IrExpression.NewException(constructorIntrinsic.Type), true);
                                int prefix = Define(offset, IrType.Reference(stringType!), new IrExpression.StringLiteral(exceptionConstruction.Prefix), true);
                                int suffix = Define(offset, IrType.Reference(stringType!), new IrExpression.StringLiteral(exceptionConstruction.Suffix), true);
                                int message = Define(offset, IrType.Reference(stringType!), new IrExpression.Call(exceptionConstruction.Concat, [prefix, constructorArguments[0], suffix]), true);
                                Push(offset, IrType.Reference(constructorIntrinsic.Type), new IrExpression.InitializeExceptionMessage(shell, message), true);
                            }
                            else
                                Push(offset, IrType.Reference(constructorIntrinsic.Type), new IrExpression.NewException(constructorIntrinsic.Type,
                                    constructorIntrinsic.MessageArgument < 0 ? null : constructorArguments[constructorIntrinsic.MessageArgument],
                                    constructorIntrinsic.ParameterArgument < 0 ? null : constructorArguments[constructorIntrinsic.ParameterArgument]), true);
                            break;
                        case "newobj" when method.Intrinsics.TryGetValue(offset, out var disposed) && disposed.Kind == ValueIntrinsic.NewObjectDisposedException:
                            int? disposedMessage = disposed.Parameters == 2 ? Pop(offset, IrType.Reference(stringType!)) : null;
                            int disposedName = Pop(offset, IrType.Reference(stringType!));
                            Push(offset, IrType.Reference(disposed.Type), new IrExpression.NewException(disposed.Type,
                                Message: disposedMessage, ObjectName: disposedName), true); break;
                        case "callvirt" when method.Intrinsics.TryGetValue(offset, out var disposedRead) && disposedRead.Kind == ValueIntrinsic.ExceptionObjectName:
                            int disposedReceiver = Pop(offset, IrType.Reference(disposedRead.Type));
                            Push(offset, IrType.Reference(stringType!), new IrExpression.ExceptionObjectName(disposedReceiver), true); break;
                        case "newobj" when method.Intrinsics.TryGetValue(offset, out var actualFault) && actualFault.Kind == ValueIntrinsic.NewArgumentOutOfRangeActual:
                            int actualMessage = Pop(offset, IrType.Reference(stringType!));
                            int actualValue = Pop(offset, IrType.Reference(objectBase));
                            int actualName = Pop(offset, IrType.Reference(stringType!));
                            if (valueExpressions[actualValue] is not IrExpression.Null
                                && (valueExpressions[actualValue] is not IrExpression.BoxValue actualBox
                                    || actualInteger is null || actualBox.Type != actualInteger))
                                Error(offset, "Actual-value range faults require null or a directly boxed selected Int32 in the same block; aliases and other payloads need later lowering.", DiagnosticIds.ExecutionUnsupported);
                            Push(offset, IrType.Reference(actualFault.Type), new IrExpression.NewException(actualFault.Type,
                                Message: actualMessage, ParameterName: actualName, ActualValue: actualValue), true); break;
                        case "newobj" when method.Intrinsics.TryGetValue(offset, out var wrapper) && wrapper.Kind is ValueIntrinsic.NewInvalidOperationInner or ValueIntrinsic.NewMessageInnerException:
                            int wrappedException = Pop(offset, IrType.Reference(exceptions!.Base));
                            int wrapperMessage = Pop(offset, IrType.Reference(stringType!));
                            Push(offset, IrType.Reference(wrapper.Type), new IrExpression.NewException(wrapper.Type,
                                Message: wrapperMessage, Inner: wrappedException), true); break;
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
                        case "call" when method.Intrinsics.TryGetValue(offset, out var copyCheck) && copyCheck.Kind == ValueIntrinsic.ArrayCopyCompatible:
                            int copyDestination = Pop(offset, IrType.Reference(copyCheck.Type));
                            int copySource = Pop(offset, IrType.Reference(copyCheck.Type));
                            Push(offset, IrType.Int32, new IrExpression.ArrayCopyCompatible(copyCheck.Type, copySource, copyDestination), true); break;
                        case "call" when method.Intrinsics.TryGetValue(offset, out var copyStore) && copyStore.Kind == ValueIntrinsic.ArrayCopyStore:
                            int copyValue = Pop(offset, ElementType(copyStore.Type));
                            int copyIndex = Pop(offset, IrType.Int32);
                            int copyArray = Pop(offset, IrType.Reference(copyStore.Type));
                            Add(new IrStatement.StoreElement(offset, copyStore.Type, copyArray, copyIndex, copyValue, CopyCast: true), offset); break;
                        case "call" when method.Intrinsics.TryGetValue(offset, out var emptyArray) && emptyArray.Kind == ValueIntrinsic.EmptyArray:
                            Push(offset, IrType.Reference(emptyArray.Type), new IrExpression.EmptyArray(emptyArray.Type), true); break;
                        case "call" when method.Intrinsics.TryGetValue(offset, out var stringConstruction) && stringConstruction.Kind == ValueIntrinsic.StringFromCodeUnits:
                            Push(offset, IrType.Reference(stringType!), new IrExpression.StringFromCodeUnits(
                                Pop(offset, IrType.Reference(ObjectTypeId.Vector(objectBase, ObjectArrayElement.Int32)))), true); break;
                        case "call" when method.Intrinsics.TryGetValue(offset, out var characterConstruction) && characterConstruction.Kind == ValueIntrinsic.StringFromCharacter:
                            Push(offset, IrType.Reference(stringType!), new IrExpression.StringFromCharacter(Pop(offset, IrType.Int32)), true); break;
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
                            bool lengthConverted = index + 1 < end && input.Instructions[index + 1].OpCode == "conv.i4";
                            bool lengthTruth = types.IntegerCasts && index + 1 < end
                                && input.Instructions[index + 1].OpCode is ("brtrue" or "brtrue.s" or "brfalse" or "brfalse.s");
                            bool lengthComparison = types.IntegerCasts && index + 2 < end
                                && input.Instructions[index + 1].OpCode == "ldc.i4.0"
                                && input.Instructions[index + 2].OpCode is ("ceq" or "cgt.un");
                            if (!lengthConverted && !lengthTruth && !lengthComparison)
                                Error(offset, "Array length requires an adjacent conv.i4, truth branch or zero comparison in the same block; other native integer flow requires later lowering.", DiagnosticIds.ExecutionUnsupported);
                            int lengthArray = PopArray(offset);
                            if (lengthConverted)
                            {
                                ++index;
                                Push(offset, IrType.Int32, new IrExpression.ArrayLength(lengthArray), true);
                            }
                            else
                            {
                                // Bounded vectors have nonnegative Int32 lengths. Their native-size
                                // zero/nonzero observation is identical on 32/64-bit targets. Materialize
                                // only that Boolean; never admit a native numeric value to
                                // arithmetic, storage or block joins. Consume the whole comparison
                                // so its constant/operator cannot become an independent entry point.
                                bool equalsZero = lengthComparison && input.Instructions[index + 2].OpCode == "ceq";
                                if (lengthComparison) index += 2;
                                int observedLength = Define(offset, IrType.Int32, new IrExpression.ArrayLength(lengthArray), true);
                                int emptyLength = Define(offset, IrType.Int32, new IrExpression.Constant(0));
                                Push(offset, IrType.Int32, new IrExpression.Compare(equalsZero ? Int32Comparison.Equal : Int32Comparison.NotEqual, observedLength, emptyLength));
                            }
                            break;
                        case "ldelema" when types.IntegerCasts:
                            int addressIndex = Pop(offset, IrType.Int32, arrayIndex: true), addressArray = PopArray(offset);
                            var addressView = ArrayView(addressArray, opcode, offset);
                            if (addressView.ArrayElement!.Kind is not (ObjectArrayElementKind.Value or ObjectArrayElementKind.Reference or ObjectArrayElementKind.Int32))
                                Error(offset, "Element addresses currently require a closed inline value, reference or Int32 array.", DiagnosticIds.ExecutionUnsupported);
                            Push(offset, IrType.Location(ElementType(addressView)), new IrExpression.AddressElement(addressView, addressArray, addressIndex), true); break;
                        case "ldelem.u4" or "ldelem.i8" or "ldelem.ref" or "ldelem" or "ldelem.u1" or "ldelem.u2" when types.IntegerCasts:
                        case "ldelem.i4" when arrays:
                            int loadIndex = Pop(offset, IrType.Int32, arrayIndex: true), loadArray = PopArray(offset);
                            var loadView = ArrayView(loadArray, opcode, offset);
                            Push(offset, ElementType(loadView),
                                new IrExpression.LoadElement(loadView, loadArray, loadIndex), true); break;
                        case "stelem.i8" or "stelem.ref" or "stelem" or "stelem.i1" or "stelem.i2" when types.IntegerCasts:
                        case "stelem.i4" when arrays:
                            int elementValue = Pop(offset), storeIndex = Pop(offset, IrType.Int32, arrayIndex: true), storeArray = PopArray(offset);
                            var storeView = ArrayView(storeArray, opcode, offset);
                            elementValue = AdaptBox(elementValue, ElementType(storeView), offset);
                            Compatible(values[elementValue], ElementType(storeView), offset);
                            Add(new IrStatement.StoreElement(offset, storeView, storeArray, storeIndex, elementValue), offset); break;
                        case "ldsfld" when types.IntegerCasts && method.DefaultStatics.TryGetValue(offset, out var defaultStatic):
                            if (defaultStatic.Kind == IrTypeKind.Reference) Push(offset, IrType.NullReference, new IrExpression.Null());
                            else Push(offset, defaultStatic, defaultStatic == IrType.Int32 ? new IrExpression.Constant(0)
                                : defaultStatic == IrType.Int64 ? new IrExpression.Constant64(0) : new IrExpression.DefaultValue(defaultStatic.Class!));
                            break;
                        case "ldsfld" when method.StaticFields.TryGetValue(offset, out var readStatic):
                            Push(offset, readStatic.Type, new IrExpression.LoadStatic(readStatic.Id), true);
                            stack[^1] = NormalizeField(stack[^1], offset); break;
                        case "ldsflda" when method.StaticFields.TryGetValue(offset, out var addressStatic):
                            Push(offset, IrType.Location(addressStatic.Type, (addressStatic.Attributes & FieldAttributes.InitOnly) != 0), new IrExpression.AddressStatic(addressStatic.Id), true); break;
                        case "stsfld" when method.StaticFields.TryGetValue(offset, out var writeStatic):
                            Add(new IrStatement.StoreStatic(offset, writeStatic.Id, NormalizeField(Pop(offset, writeStatic.Type), offset)), offset); break;
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
                            Push(offset, IrType.Location(method.Locals[localAddress], narrow: IntegerCasts.NarrowStorage(input.Locals[localAddress])),
                                new IrExpression.AddressLocal(localAddress)); break;
                        case "ldarga" or "ldarga.s":
                            int parameterAddress = Variable(instruction, method.Parameters.Length);
                            if (method.Parameters[parameterAddress].Kind == IrTypeKind.ManagedLocation)
                                Error(offset, "Taking the address of a borrowed parameter would create a nested byref.", DiagnosticIds.ExecutionUnsupported);
                            Push(offset, IrType.Location(method.Parameters[parameterAddress], narrow: ParameterStorage(parameterAddress) is { } parameterStorage
                                ? IntegerCasts.NarrowStorage(parameterStorage) : null), new IrExpression.AddressParameter(parameterAddress)); break;
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
                        case "ldftn" or "ldvirtftn" when delegates.Count != 0:
                            if (index + 1 >= end || input.Instructions[index + 1] is not { OpCode: "newobj" } next
                                || !method.DelegateOperands.ContainsKey(next.Offset))
                                Error(offset, "ldftn requires an adjacent delegate constructor in the same block; escaping method pointers and branch entries into construction are unsupported.", DiagnosticIds.ExecutionUnsupported);
                            bool virtualLookup = opcode == "ldvirtftn";
                            int lookupReceiver = -1;
                            if (virtualLookup)
                            {
                                if (!types.IntegerCasts || index <= start || input.Instructions[index - 1].OpCode != "dup")
                                    Error(offset, "Final virtual delegate capture requires adjacent dup/ldvirtftn/newobj in one block.", DiagnosticIds.ExecutionUnsupported);
                                lookupReceiver = Pop(offset);
                            }
                            var construction = input.Instructions[++index];
                            var callableType = method.DelegateOperands[construction.Offset];
                            int capture = Pop(offset);
                            if (!Reference(values[capture])) Error(offset, "Delegate capture requires a reference receiver or null.");
                            if (virtualLookup && capture != lookupReceiver)
                                Error(offset, "Final virtual delegate lookup and capture must use the same receiver.");
                            Push(construction.Offset, IrType.Reference(callableType), new IrExpression.NewDelegate(callableType, method.Calls[offset], capture)
                                { VirtualLookupOffset = virtualLookup ? offset : null }, true);
                            break;
                        case "callvirt" when method.DelegateOperands.ContainsKey(offset):
                            var delegateId = method.DelegateOperands[offset]; var declaration = delegates[delegateId];
                            var delegateArguments = new int[declaration.ParameterCount];
                            for (int i = delegateArguments.Length - 1; i >= 0; --i) delegateArguments[i] = Pop(offset, declaration.Parameters[i]);
                            int calledDelegate = Pop(offset, IrType.Reference(delegateId));
                            if (declaration.ReturnsVoid) Add(new IrStatement.InvokeDelegate(offset, delegateId, calledDelegate, [.. delegateArguments]), offset);
                            else Push(offset, declaration.ReturnType, new IrExpression.CallDelegate(delegateId, calledDelegate, [.. delegateArguments]), true);
                            break;
                        case "newobj" when method.DelegateOperands.ContainsKey(offset):
                            Error(offset, "Delegate constructor requires a preceding adjacent ldftn in the same block.", DiagnosticIds.ExecutionUnsupported); break;
                        case "callvirt" when method.AbstractOperands.TryGetValue(offset, out var abstractDeclaration):
                            var abstractArguments = new int[abstractDeclaration.Parameters.Length];
                            for (int i = abstractArguments.Length - 1; i >= 0; --i) abstractArguments[i] = Pop(offset, abstractDeclaration.Parameters[i]);
                            var abstractMethod = IrMethodId.From(abstractDeclaration.Source);
                            if (abstractDeclaration.ReturnType == IrType.Void) Add(new IrStatement.Invoke(offset, abstractMethod, [.. abstractArguments], NullCheckReceiver: true, VirtualDispatch: true), offset);
                            else Push(offset, abstractDeclaration.ReturnType, new IrExpression.Call(abstractMethod, [.. abstractArguments], NullCheckReceiver: true, VirtualDispatch: true), true);
                            break;
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
                        case "box" or "unbox.any" when boxing && method.Calls.ContainsKey(offset):
                            var target = methods[method.Calls[offset]];
                            if (method.OrdinalSortCalls.TryGetValue(offset, out var ordinalSort))
                            {
                                if (opcode != "callvirt" || target.Binding.Kind != ObjectMethodKind.Instance
                                    || target.Binding.Owner != ordinalSort.Owner || target.ReturnType != IrType.Void
                                    || target.Parameters.Length != 1 || target.Parameters[0] != IrType.Reference(ordinalSort.Owner))
                                    Error(offset, "Ordinal list sort requires its exact managed instance bridge.");
                                int comparer = Pop(offset);
                                if (valueExpressions[comparer] is not IrExpression.Call { Arguments.IsEmpty: true } proof || proof.Target != ordinalSort.Getter)
                                    Error(offset, "List.Sort(IComparer<T>) currently requires List<string> and a directly proven StringComparer.Ordinal getter in the same block; null, custom comparers and aliases require later lowering.", DiagnosticIds.ExecutionUnsupported);
                            }
                            if (method.OrdinalPrefixCalls.Contains(offset) || method.OrdinalCompareCalls.Contains(offset))
                            {
                                bool prefixBridge = method.OrdinalPrefixCalls.Contains(offset);
                                if ((prefixBridge ? opcode is not ("call" or "callvirt") : opcode != "call")
                                    || target.Binding.Kind != (prefixBridge ? ObjectMethodKind.Instance : ObjectMethodKind.Static)
                                    || target.Binding.Owner != stringType || target.ReturnType != IrType.Int32
                                    || target.Parameters.Length != 2 || target.Parameters.Any(p => p != IrType.Reference(stringType!)))
                                    Error(offset, "Ordinal text bridge requires its exact selected String signature.");
                                int prefixMode = Pop(offset, IrType.Int32);
                                if (valueExpressions[prefixMode] is not IrExpression.Constant { Value: 4 })
                                    Error(offset, "Ordinal text calls require a directly proven StringComparison.Ordinal constant; other modes and aliases require later lowering.", DiagnosticIds.ExecutionUnsupported);
                            }
                            if (method.OrdinalOrderingGetters.TryGetValue(offset, out var orderingGetter))
                            {
                                if (opcode != "call" || stack.Count == 0 || valueExpressions[stack[^1]] is not IrExpression.Call { Arguments.IsEmpty: true } orderingProof || orderingProof.Target != orderingGetter)
                                    Error(offset, "Ordinal ordering requires a directly proven StringComparer.Ordinal getter in the same block; null, custom comparers and aliases require later lowering.", DiagnosticIds.ExecutionUnsupported);
                            }
                            if (method.OrdinalCollectionGetters.TryGetValue(offset, out var ordinalCollectionGetter))
                            {
                                if (stack.Count == 0 || valueExpressions[stack[^1]] is not IrExpression.Call { Arguments.IsEmpty: true } setProof || setProof.Target != ordinalCollectionGetter)
                                    Error(offset, "Ordinal collection construction requires a directly proven StringComparer.Ordinal getter in the same block; null, custom comparers and aliases require later lowering.", DiagnosticIds.ExecutionUnsupported);
                            }
                            bool factoryConstructor = method.FactoryConstructorCalls.TryGetValue(offset, out var factoryOwner);
                            if (factoryConstructor && (opcode != "newobj" || target.Binding.Kind != ObjectMethodKind.Static
                                || target.Binding.Owner != factoryOwner || target.ReturnType != IrType.Reference(factoryOwner!)
                                || (target.Parameters.Length != 1 && !(method.OrdinalCollectionGetters.ContainsKey(offset) && target.Parameters.Length == 2 && target.Parameters[0].Kind is IrTypeKind.Int32 or IrTypeKind.Reference))))
                                Error(offset, "Constructor factory requires its exact checked static signature.");
                            int skip = opcode == "newobj" && !factoryConstructor ? 1 : 0;
                            bool constrained = method.ConstrainedCalls.TryGetValue(offset, out var constrainedCall);
                            if (constrained && (target.Binding.Kind != ObjectMethodKind.ValueInstance || target.Binding.Owner != constrainedCall!.Receiver))
                                Error(offset, "Constrained call does not match its exact checked value receiver.");
                            bool nullCheck = opcode == "callvirt" && !constrained;
                            // Final class methods keep callvirt null semantics but have a fixed body.
                            bool virtualDispatch = nullCheck && target.Binding.VirtualSlot is not null
                                && (target.Binding.Attributes & System.Reflection.MethodAttributes.Final) == 0;
                            // The authenticated ordinary renderer ignores format text. Consume its
                            // original call operands before adapting the exact payload receiver.
                            if (method.BoxedRenderCalls.ContainsKey(offset))
                            { Pop(offset, IrType.Int32); Pop(offset, IrType.Int32); Pop(offset, IrType.Reference(stringType!)); }
                            var arguments = new int[target.Parameters.Length - skip];
                            for (int i = arguments.Length - 1; i >= 0; i--)
                            {
                                if (i == 0 && method.BoxedRenderCalls.TryGetValue(offset, out var boxedReceiver))
                                {
                                    if (skip != 0 || arguments.Length != 1 || target.Binding.Kind != ObjectMethodKind.ValueInstance
                                        || target.Binding.Owner != boxedReceiver || target.Parameters[0] != IrType.Location(IrType.Value(boxedReceiver), target.Binding.ReadOnlyReceiver))
                                        Error(offset, "Boxed text call requires its exact payload receiver.");
                                    int box = Pop(offset, IrType.Reference(objectBase));
                                    arguments[0] = Define(offset, IrType.Location(IrType.Value(boxedReceiver)), new IrExpression.UnboxLocation(boxedReceiver, box), true);
                                }
                                else if (factoryConstructor)
                                {
                                    int source = Pop(offset);
                                    if (!types.Assignable(values[source], target.Parameters[i]))
                                        Error(offset, factoryOwner == stringType ? "Character-string constructor requires the exact Char[] source or null."
                                            : "Collection constructor requires arguments assignable to its exact selected factory signature.", DiagnosticIds.ExecutionUnsupported);
                                    arguments[i] = source;
                                }
                                else arguments[i] = Pop(offset, target.Parameters[i + skip]);
                            }
                            if (method.OrdinalPrefixCalls.Contains(offset) && opcode == "call" && valueExpressions[arguments[0]] is not IrExpression.StringLiteral)
                                Error(offset, "Direct String.StartsWith calls require a proven non-null literal receiver; other direct calls have JIT-dependent null behavior and require later lowering.", DiagnosticIds.ExecutionUnsupported);
                            if (method.BoxedFormatCalls.TryGetValue(offset, out var formatBoxes)
                                && (formatBoxes.Length is not (1 or 2 or 3) || arguments.Length != formatBoxes.Length + 1
                                    || formatBoxes.Where((box, i) => box == stringType
                                        ? values[arguments[i + 1]] != IrType.Reference(stringType!) && values[arguments[i + 1]] != IrType.NullReference
                                        : (valueExpressions[arguments[i + 1]] is not IrExpression.BoxValue proof || proof.Type != box)
                                            && (!nullableBoxes.TryGetValue(arguments[i + 1], out var payload) || payload != box)).Any()))
                                Error(offset, "String.Format requires each directly proven box or typed string in the same control-flow block.", DiagnosticIds.ExecutionUnsupported);
                            if (method.LiteralIntegerFormats.Contains(offset) && (valueExpressions[arguments[0]] is not IrExpression.StringLiteral formatLiteral
                                || !DefaultIntegerFormat.Literal(formatLiteral.CodeUnits)))
                                Error(offset, "Integer composite formatting requires a directly proven literal without format suffixes in the same block.", DiagnosticIds.ExecutionUnsupported);
                            if (target.Binding.IntegerParser is { } parser)
                            {
                                bool implicitStyle = target.Binding.ThrowingIntegerParser && (parser.Code == PrimitiveTypeCode.Int64 || target.Parameters.Length == 2);
                                if (arguments.Length != (implicitStyle ? 2 : target.Binding.ThrowingIntegerParser ? 3 : 4)
                                    || !implicitStyle && (valueExpressions[arguments[1]] is not IrExpression.Constant parserStyle
                                        || !IntegerParserRules.AllowsStyle(parser.Code, parserStyle.Value))
                                    || valueExpressions[arguments[implicitStyle ? 1 : 2]] is not IrExpression.InvariantCulture)
                                    Error(offset, "Integer parsing requires the selected fixed grammar and directly proven CultureInfo.InvariantCulture.", DiagnosticIds.ExecutionUnsupported);
                            }
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
                            else if (target.ReturnType == IrType.Void) Add(new IrStatement.Invoke(offset, target.Binding.Method, [.. arguments], NullCheckReceiver: nullCheck, VirtualDispatch: virtualDispatch), offset);
                            else
                            {
                                Push(offset, target.ReturnType, new IrExpression.Call(target.Binding.Method, [.. arguments], NullCheckReceiver: nullCheck, VirtualDispatch: virtualDispatch) { ValueKeyContract = method.ValueKeyCalls.GetValueOrDefault(offset) }, true);
                                // Only the exact metadata-validated CIL box gets this fact.
                                // Locals, joins, arbitrary calls and direct helper calls do not.
                                if (opcode == "box" && method.NullableBoxPayloads.TryGetValue(offset, out var payload))
                                    nullableBoxes.Add(stack[^1], payload);
                            }
                            break;
                        case "ldfld" or "ldflda" or "stfld":
                            var field = fields[method.Fields[offset]]; var fieldType = Storage(field.Storage);
                            int fieldValue = opcode == "stfld" ? NormalizeField(Pop(offset, fieldType), offset) : -1;
                            int receiver = Pop(offset); var receiverType = values[receiver];
                            if (classes && types.Valid(IrType.Reference(field.Id.Owner), storage: true))
                            {
                                Compatible(receiverType, IrType.Reference(field.Id.Owner), offset);
                                if (opcode == "ldfld") Push(offset, fieldType, new IrExpression.LoadField(field.Id, receiver), true);
                                else if (opcode == "stfld")
                                {
                                    if (fieldType.Kind == IrTypeKind.Value && valueExpressions[fieldValue] is IrExpression.Call)
                                    {
                                        if (!types.IntegerCasts)
                                            Error(offset, NonNullFieldReturnProof.Unsupported, DiagnosticIds.ExecutionUnsupported);
                                        returnedFields.Add((id, receiver, offset));
                                    }
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
                            if (opcode == "ldfld") stack[^1] = NormalizeField(stack[^1], offset);
                            break;
                        case "box" or "unbox.any" when boxing && method.ReferenceBoxOperands.TryGetValue(offset, out var referenceBox):
                            int referenceOperand = Pop(offset, opcode == "box" ? referenceBox : null);
                            if (!Reference(values[referenceOperand])) Error(offset, "Reference boxing/casting requires an object reference or null.");
                            if (opcode == "box") stack.Add(referenceOperand);
                            else Push(offset, referenceBox, new IrExpression.CastClass(referenceBox.Class!, referenceOperand), true);
                            break;
                        case "box" or "unbox" or "unbox.any" when boxing:
                            var boxedType = method.TypeOperands[offset];
                            var boxStorage = method.ScalarBoxOperands.TryGetValue(offset, out var scalarBox) ? ScalarBoxing.Stack(scalarBox) : IrType.Value(boxedType);
                            int boxedOperand = opcode == "box" ? Pop(offset, boxStorage) : Pop(offset);
                            if (opcode != "box" && !Reference(values[boxedOperand])) Error(offset, "Unboxing requires an object reference or null.");
                            if (opcode == "box") Push(offset, IrType.Reference(objectBase), new IrExpression.BoxValue(boxedType, boxedOperand), true);
                            else if (opcode == "unbox") Push(offset, IrType.Location(IrType.Value(boxedType)), new IrExpression.UnboxLocation(boxedType, boxedOperand), true);
                            else Push(offset, boxStorage, new IrExpression.UnboxValue(boxedType, boxedOperand) { NullableTarget = method.NullableUnboxOperands.Contains(offset) }, true);
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
                        case "ldind.i1" or "ldind.u1" or "ldind.i2" or "ldind.u2" when types.IntegerCasts:
                            int narrowRead = Location(offset, IrType.Int32, allowNarrow: true);
                            var readCast = opcode switch { "ldind.i1" => IntegerCastTarget.Int8, "ldind.u1" => IntegerCastTarget.UInt8,
                                "ldind.i2" => IntegerCastTarget.Int16, _ => IntegerCastTarget.UInt16 };
                            if (values[narrowRead].Narrow is not { } readWidth || IntegerCasts.Bits(readWidth) != IntegerCasts.Bits(readCast))
                                Error(offset, "Narrow indirect load requires a matching storage width.");
                            int narrowLoaded = Define(offset, IrType.Int32, new IrExpression.LoadLocation(narrowRead), true);
                            Push(offset, IrType.Int32, new IrExpression.IntegerCast(readCast, narrowLoaded, false, false)); break;
                        case "stind.i1" or "stind.i2" when types.IntegerCasts:
                            int narrowValue = Pop(offset, IrType.Int32), narrowWrite = Location(offset, IrType.Int32, allowNarrow: true);
                            if (values[narrowWrite].Narrow is not { } writeWidth || IntegerCasts.Bits(writeWidth) != (opcode == "stind.i1" ? 8 : 16))
                                Error(offset, "Narrow indirect store requires a matching storage width.");
                            Writable(narrowWrite, offset);
                            Add(new IrStatement.StoreLocation(offset, narrowWrite, narrowValue), offset); break;
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
                        case "switch" when types.IntegerCasts:
                            int selector = Pop(offset, IrType.Int32);
                            if (instruction.Targets.IsEmpty)
                                terminator = new IrJump(offset, Edge(instruction.Offset + instruction.Length, offset));
                            else
                            {
                                int selected = Define(offset, IrType.Int32, new IrExpression.Constant(switchCase));
                                var match = Edge(instruction.Targets[switchCase], offset);
                                IrEdge nextCase;
                                if (switchCase + 1 == instruction.Targets.Length)
                                    nextCase = Edge(instruction.Offset + instruction.Length, offset);
                                else
                                {
                                    stack.Add(selector); nextCase = EdgeToBlock(id + 1, offset); stack.RemoveAt(stack.Count - 1);
                                }
                                terminator = new IrBranch(offset, Int32Comparison.Equal, selector, selected, match, nextCase);
                            }
                            break;
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
                if (nativeIndices.Count != 0) Error(blockOffset, "Checked native indices require one array element use in the same block.", DiagnosticIds.ExecutionUnsupported);
                var last = input.Instructions[end - 1];
                terminator ??= new IrJump(last.Offset, Edge(last.Offset + last.Length, last.Offset));
                return new(id, blockOffset, [.. incoming[id].Where((_, index) => incomingSources[id][index] is null)], [.. statements], terminator);

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
                int NormalizeField(int operand, int offset) => method.NarrowFields.TryGetValue(offset, out var target)
                    ? Define(offset, IrType.Int32, new IrExpression.IntegerCast(target, operand, false, false)) : operand;
                int Pop(int offset, IrType? expected = null, bool arrayIndex = false)
                {
                    if (stack.Count == 0) Error(offset, "Instruction requires an evaluation-stack operand.");
                    int id = stack[^1]; stack.RemoveAt(stack.Count - 1);
                    if (nativeIndices.Contains(id))
                    {
                        if (!arrayIndex) Error(offset, "Checked native indices support only array element access; arithmetic, aliases and general native-integer flow require later lowering.", DiagnosticIds.ExecutionUnsupported);
                        nativeIndices.Remove(id);
                    }
                    if (expected is not null) { id = AdaptBox(id, expected, offset); Compatible(values[id], expected, offset); } return id;
                }
                int AdaptBox(int id, IrType expected, int offset)
                {
                    if (expected.Kind == IrTypeKind.Reference && !types.Assignable(values[id], expected)
                        && valueExpressions[id] is IrExpression.BoxValue box && boxedRelations?.Accepts(box.Type, expected.Class!) == true)
                        return Define(offset, expected, new IrExpression.CastClass(expected.Class!, id), true);
                    return id;
                }
                int Location(int offset, IrType? element, bool allowNarrow = false)
                {
                    int id = Pop(offset);
                    if (values[id].Kind != IrTypeKind.ManagedLocation || element is not null && values[id].Element != element
                        || !allowNarrow && values[id].Narrow is not null)
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
                static IrType ElementType(ObjectTypeId array) => array.ArrayElement!.Kind switch {
                    ObjectArrayElementKind.Boolean or ObjectArrayElementKind.Int32 or ObjectArrayElementKind.UInt32 or ObjectArrayElementKind.Byte or ObjectArrayElementKind.Char or ObjectArrayElementKind.Enum32 => IrType.Int32,
                    ObjectArrayElementKind.Int64 => IrType.Int64,
                    ObjectArrayElementKind.Value => IrType.Value(array.ArrayElement.Type!),
                    _ => IrType.Reference(array.ArrayElement.Type!) };
                ObjectTypeId ArrayView(int receiver, string opcode, int offset)
                {
                    if (method.ArrayOperands.TryGetValue(offset, out var declared))
                    {
                        Compatible(values[receiver], IrType.Reference(declared), offset); return declared;
                    }
                    var expected = opcode switch {
                        "ldelem.i8" or "stelem.i8" => ObjectArrayElement.Int64,
                        "ldelem.u1" or "stelem.i1" => ObjectArrayElement.Byte,
                        "ldelem.u2" or "stelem.i2" => ObjectArrayElement.Char,
                        "ldelem.ref" or "stelem.ref" => ObjectArrayElement.Reference(objectBase),
                        _ => ObjectArrayElement.Int32 };
                    var view = values[receiver] == IrType.NullReference
                        ? ObjectTypeId.Vector(objectBase, expected) : values[receiver].Class!;
                    if (view.ArrayElement!.Kind != expected.Kind
                        && !(expected.Kind == ObjectArrayElementKind.Int32 && view.ArrayElement.Kind is ObjectArrayElementKind.Enum32 or ObjectArrayElementKind.UInt32)
                        && !(expected.Kind == ObjectArrayElementKind.Byte && view.ArrayElement.Kind == ObjectArrayElementKind.Boolean))
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
                    return EdgeToBlock(target, from);
                }
                IrEdge EdgeToBlock(int target, int from)
                {
                    if (nativeIndices.Count != 0) Error(from, "Checked native indices cannot cross block boundaries.", DiagnosticIds.ExecutionUnsupported);
                    var stackTypes = stack.Select(id => values[id]).ToImmutableArray();
                    var sources = stack.Select(value =>
                    {
                        var expression = valueExpressions[value];
                        // Preserve constructor-this identity for readonly-field initialization,
                        // and legacy receiver rematerialization. Integer-object flow
                        // transfers checked location packets instead: distinct predecessors
                        // may select different slots without copying the pointed-to value.
                        if ((stableConstructorReceiver || method.Binding.Kind == ObjectMethodKind.ValueConstructor
                            || !types.IntegerCasts && method.Binding.Kind is (ObjectMethodKind.ValueInstance or ObjectMethodKind.PrimitiveInstance))
                            && expression is IrExpression.Parameter { Index: 0 }) return expression;
                        return null;
                    }).ToImmutableArray();
                    if (!types.IntegerCasts && stackTypes.Where((type, index) => type.Kind == IrTypeKind.ManagedLocation && sources[index] is null).Any())
                        Error(from, "Managed locations cannot cross block joins in this profile.", DiagnosticIds.ExecutionUnsupported);
                    if (!incoming[target].IsDefault && !incoming[target].SequenceEqual(stackTypes))
                    {
                        if (exceptions is null || incoming[target].Length != stackTypes.Length) Error(from, "Block receives incompatible stack types or depths.");
                        var joined = incoming[target].Select((prior, index) => types.Join(prior, stackTypes[index])).ToArray();
                        if (joined.Any(t => t is null)) Error(from, "Reference join has no unique most-specific common type; use a typed local or explicit cast.");
                        var widened = joined.Select(t => t!).ToImmutableArray();
                        if (!incoming[target].SequenceEqual(widened)) { incoming[target] = widened; pending.Enqueue(target); }
                    }
                    if (incoming[target].IsDefault) { incoming[target] = stackTypes; incomingSources[target] = sources; pending.Enqueue(target); }
                    else if (!incomingSources[target].SequenceEqual(sources)) Error(from, "Rematerialized receiver/location identity differs across incoming edges.");
                    _edges += stackTypes.Length;
                    if (_edges > IrLimits.MaximumEdgeArguments) Error(from, "Aggregate edge-argument limit exceeded.", DiagnosticIds.TranslationLimit);
                    return new(target, [.. stack.Where((_, index) => sources[index] is null)]);
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
