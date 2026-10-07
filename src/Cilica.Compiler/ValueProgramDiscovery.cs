using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

/// <summary>Metadata-backed method inputs. Bodies have not yet been stack-verified or translated.</summary>
public sealed record ValueMethodInput(MethodInput Input, ObjectMethodBinding Binding,
    ImmutableArray<IrType> Parameters, IrType ReturnType, ImmutableArray<IrType> Locals,
    ImmutableDictionary<int, IrMethodId> Calls, ImmutableDictionary<int, ObjectFieldId> Fields,
    ImmutableDictionary<int, ObjectTypeId> TypeOperands)
{
    public ImmutableDictionary<int, ObjectAbstractMethodDeclaration> AbstractOperands { get; init; } = ImmutableDictionary<int, ObjectAbstractMethodDeclaration>.Empty;
    public GenericArrayEvidence? GenericArrays { get; init; }
    public ImmutableDictionary<int, PrimitiveTypeCode> ScalarBoxOperands { get; init; } = ImmutableDictionary<int, PrimitiveTypeCode>.Empty;
    public ImmutableHashSet<int> BoxedTypeTestOperands { get; init; } = ImmutableHashSet<int>.Empty;
    public ImmutableHashSet<int> NullableUnboxOperands { get; init; } = ImmutableHashSet<int>.Empty;
    public ImmutableDictionary<int, StaticArrayInitialization> StaticArrays { get; init; } = ImmutableDictionary<int, StaticArrayInitialization>.Empty;
    public ImmutableDictionary<int, RuntimeTypeToken> RuntimeTypeTokens { get; init; } = ImmutableDictionary<int, RuntimeTypeToken>.Empty;
    public ImmutableArray<IrExceptionRegion> ExceptionRegions { get; init; } = [];
    public ImmutableDictionary<int, ValueIntrinsicCall> Intrinsics { get; init; } = ImmutableDictionary<int, ValueIntrinsicCall>.Empty;
    public ImmutableDictionary<int, ImmutableArray<ushort>> Literals { get; init; } = ImmutableDictionary<int, ImmutableArray<ushort>>.Empty;
    public ImmutableDictionary<int, ObjectTypeId> DelegateOperands { get; init; } = ImmutableDictionary<int, ObjectTypeId>.Empty;
    public ImmutableDictionary<int, ObjectTypeId> ArrayOperands { get; init; } = ImmutableDictionary<int, ObjectTypeId>.Empty;
    public ImmutableDictionary<int, ObjectInterfaceDeclaration> InterfaceOperands { get; init; } = ImmutableDictionary<int, ObjectInterfaceDeclaration>.Empty;
    public ImmutableDictionary<int, IrType> MemoryOperands { get; init; } = ImmutableDictionary<int, IrType>.Empty;
    public ImmutableDictionary<int, IrMethodId> Initializers { get; init; } = ImmutableDictionary<int, IrMethodId>.Empty;
    public ImmutableDictionary<int, ObjectStaticField> StaticFields { get; init; } = ImmutableDictionary<int, ObjectStaticField>.Empty;
    public ImmutableDictionary<int, IrType> DefaultStatics { get; init; } = ImmutableDictionary<int, IrType>.Empty;
    public ImmutableDictionary<int, IntegerCastTarget> NarrowFields { get; init; } = ImmutableDictionary<int, IntegerCastTarget>.Empty;
    public ImmutableDictionary<int, IrType> ReferenceBoxOperands { get; init; } = ImmutableDictionary<int, IrType>.Empty;
    public ImmutableDictionary<int, ClosedKeyIntrinsic> KeyOperations { get; init; } = ImmutableDictionary<int, ClosedKeyIntrinsic>.Empty;
    public ImmutableDictionary<int, ObjectTypeId> BoxedRenderCalls { get; init; } = ImmutableDictionary<int, ObjectTypeId>.Empty;
    public ImmutableDictionary<int, ImmutableArray<ObjectTypeId>> BoxedFormatCalls { get; init; } = ImmutableDictionary<int, ImmutableArray<ObjectTypeId>>.Empty;
    public ImmutableDictionary<int, ObjectTypeId> FactoryConstructorCalls { get; init; } = ImmutableDictionary<int, ObjectTypeId>.Empty;
    public ImmutableDictionary<int, ConstrainedValueCall> EnumCompareCalls { get; init; } = ImmutableDictionary<int, ConstrainedValueCall>.Empty;
    public ImmutableDictionary<int, OrdinalListSortCall> OrdinalSortCalls { get; init; } = ImmutableDictionary<int, OrdinalListSortCall>.Empty;
    public ImmutableDictionary<int, IrMethodId> OrdinalCollectionGetters { get; init; } = ImmutableDictionary<int, IrMethodId>.Empty;
    public ImmutableHashSet<int> LiteralIntegerFormats { get; init; } = ImmutableHashSet<int>.Empty;
    public ImmutableHashSet<int> OrdinalCompareCalls { get; init; } = ImmutableHashSet<int>.Empty;
    public ImmutableHashSet<int> OrdinalPrefixCalls { get; init; } = ImmutableHashSet<int>.Empty;
    public ImmutableDictionary<int, ConstrainedReferenceCall> ConstrainedReferenceCalls { get; init; } = ImmutableDictionary<int, ConstrainedReferenceCall>.Empty;
    public ImmutableDictionary<int, ConstrainedValueCall> ConstrainedCalls { get; init; } = ImmutableDictionary<int, ConstrainedValueCall>.Empty;
}
public sealed record OrdinalListSortCall(IrMethodId Getter, ObjectTypeId Owner);
public sealed record ConstrainedValueCall(int PrefixOffset, ObjectTypeId Receiver);
public sealed record ConstrainedReferenceCall(int PrefixOffset, ObjectTypeId Receiver, int ParameterCount);

/// <summary>A bounded metadata discovery result, not an executable program or access-proof supplied by a caller.</summary>
public enum ValueIntrinsic { NewException, ExceptionMessage, StringLength, StringEqual, StringCodeUnit, StringConcat, StringNotEqual, ExceptionParameterName, InvariantCulture, IntegerTextFind, IntegerTextPublish, StringFromCodeUnits, EmptyArray, NewTypeInitializationException, ExceptionInnerException, ExceptionTypeName, NewArgumentOutOfRangeActual, ArrayCopyStore, ArrayCopyCompatible, StringOrdinalEqual, ObjectGetType, RuntimeTypeHash, ObjectIdentityHash, NewInvalidOperationInner, EnumQuery, EnumErrorMessage, EnumBits, EnumCount, EnumValueAt, EnumNameAt, ClosedTupleShape, NewObjectDisposedException, ExceptionObjectName }
public sealed record ExceptionMessageConstruction(IrMethodId Concat, ImmutableArray<ushort> Prefix, ImmutableArray<ushort> Suffix);
public sealed record ValueIntrinsicCall(ValueIntrinsic Kind, ObjectTypeId Type, int Parameters,
    int MessageArgument = -1, int ParameterArgument = -1)
{
    public ExceptionMessageConstruction? ExceptionMessage { get; init; }
    public ObjectTypeId? RuntimeTypeBase { get; init; }
    public int ConstantValue { get; init; }
}
public sealed record ValueProgramInput(IrMethodId Entry, ImmutableArray<ValueMethodInput> Methods, ValueTypeCatalog Types)
{
    public ManagedExceptionTypes? Exceptions { get; init; }
    public ObjectInterfaceDispatchPlan? InterfaceDispatch { get; init; }
    public ImmutableArray<ObjectMethodBinding> AbstractBindings { get; init; } = [];
    public ImmutableArray<ObjectAbstractMethodDeclaration> AbstractMethods { get; init; } = [];
    public ImmutableArray<ObjectVirtualDispatch> Dispatch { get; init; } = [];
}
public sealed record ValueDiscoveryResult(ValueProgramInput? Input, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Input is not null;
}

/// <summary>Discovers bounded CIL value closures with opt-in class constructors without executing assemblies.</summary>
public static partial class ValueProgramDiscovery
{
    public static ValueDiscoveryResult Read(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits = null) =>
        ReadCore(resolver, assembly, entry, runtimeLibrary, limits, arrays: false);

    public static ValueDiscoveryResult ReadArrays(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits = null) =>
        ReadCore(resolver, assembly, entry, runtimeLibrary, limits, arrays: true);

    public static ValueDiscoveryResult ReadObjects(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits = null) =>
        ReadCore(resolver, assembly, entry, runtimeLibrary, limits, arrays: true, classes: true);

    public static ValueDiscoveryResult ReadBoxes(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits = null) =>
        ReadCore(resolver, assembly, entry, runtimeLibrary, limits, arrays: true, classes: true, boxing: true);

    public static ValueDiscoveryResult ReadGenerics(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits = null) =>
        ReadCore(resolver, assembly, entry, runtimeLibrary, limits, arrays: true, classes: true, boxing: true, generics: true);

    public static ValueDiscoveryResult ReadDelegates(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits = null) =>
        ReadCore(resolver, assembly, entry, runtimeLibrary, limits, arrays: true, classes: true, boxing: true, generics: true, delegates: true);

    public static ValueDiscoveryResult ReadExceptions(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits = null) =>
        ReadCore(resolver, assembly, entry, runtimeLibrary, limits, arrays: true, classes: true, boxing: true, generics: true, delegates: true, exceptions: true);

    public static ValueDiscoveryResult ReadIntegerObjects(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits = null) =>
        ReadCore(resolver, assembly, entry, runtimeLibrary, limits, arrays: true, classes: true, boxing: true, generics: true, delegates: true, exceptions: true, integers64: true);

    private static ValueDiscoveryResult ReadCore(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits, bool arrays, bool classes = false, bool boxing = false, bool generics = false, bool delegates = false, bool exceptions = false, bool integers64 = false)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(runtimeLibrary);
        try { return new(new Session(resolver, runtimeLibrary, limits ?? new(), arrays, classes, boxing, generics, delegates, exceptions, integers64).Read(assembly, entry), []); }
        catch (Failure failure) { return new(null, [failure.Diagnostic]); }
    }

    private sealed class Failure(Diagnostic diagnostic) : Exception(diagnostic.Message)
    {
        internal Diagnostic Diagnostic { get; } = diagnostic;
    }

    private sealed partial class Session(AssemblyResolver resolver, ResolvedAssembly library, MethodReadLimits limits, bool arrays, bool classes, bool boxing, bool generics, bool delegates, bool exceptions, bool integers64)
    {
        private sealed record Work(ResolvedAssembly Assembly, ResolvedMethod Resolved, MethodInput Input, ObjectMethodBinding Binding,
            ImmutableArray<IrType> Parameters, IrType ReturnType, ImmutableArray<IrType> Locals, Work? Parent, int Offset) { internal ImmutableArray<IrExceptionRegion> Regions { get; init; } = []; }
        private readonly MemberResolutionBudget _metadata = new(ObjectModelLimits.MaximumMetadataMembers);
        private readonly Dictionary<IrMethodId, Work> _methods = [];
        private readonly Queue<Work> _pending = new();
        private readonly Dictionary<ObjectTypeId, ConstructedType> _values = [];
        private readonly Dictionary<ObjectTypeId, ConstructedType> _references = [];
        private readonly HashSet<ObjectTypeId> _vectors = [];
        private readonly Dictionary<ObjectTypeId, TypeInput> _snapshots = [];
        private readonly Dictionary<ObjectTypeId, ConstructedType> _knownTypes = [];
        private readonly Dictionary<IrMethodId, (ResolvedMethod Method, Work Caller, int Offset)> _interfaces = [];
        private ObjectInterfacePlanBuilder.Session? _interfaceSession;
        private readonly Dictionary<IrMethodId, (Work Caller, int Offset)> _virtualCalls = [];
        private readonly Dictionary<(IrMethodId Declaration, ObjectTypeId Receiver), ClosedClassVirtualTarget> _virtualCache = [];
        private readonly Dictionary<IrMethodId, Dictionary<ObjectTypeId, IrMethodId>> _dispatch = [];
        private int _dispatchTargetCount;
        // A generic operand is meaningful only in its exact closed caller context.
        // Cache declarations, never access decisions or intrinsic authorization.
        private readonly Dictionary<(string Hash, int Token, IrMethodId? Caller), ResolvedMethod> _calls = [];
        private readonly Dictionary<(string Hash, int Token, IrMethodId? Caller), ResolvedField> _fields = [];
        private readonly Dictionary<(string Hash, int Token), ResolvedType> _types = [];
        private readonly Dictionary<ObjectTypeId, PrimitiveTypeCode> _scalarBoxes = [];
        private int _bytes, _instructions, _parameters, _locals, _arguments, _snapshotFields;
        private ObjectTypeId? _integerArray, _integer, _object, _valueBase;

        private ObjectTypeId ObjectBase() => _object ??= ObjectModelBuilder.Id(Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Object)));

        private ObjectTypeId IntegerArray() => _integerArray ??= ObjectTypeId.Vector(
            ObjectBase(), ObjectArrayElement.Int32);

        internal ValueProgramInput Read(ResolvedAssembly assembly, MethodDefinitionHandle entry)
        {
            if (exceptions) InitializeExceptions();
            var resolved = Require(resolver.ResolveDeclaredMethod(assembly, entry, _metadata));
            var root = Discover(resolved, null, 0);
            if (root.Binding.Kind != ObjectMethodKind.Static || root.ReturnType != IrType.Int32 || root.Parameters.Any(t => t != IrType.Int32))
                Fail(root, "Value entry requires Int32 parameters and return; values and borrows remain internal.");
            var methods = new List<ValueMethodInput>();
            ValueTypeCatalog catalog;
            ObjectInterfaceDispatchPlan? interfacePlan = null;
            while (true)
            {
                while (_pending.TryDequeue(out var work))
                {
                    var staticArrays = integers64 ? StaticArrays(work) : ImmutableDictionary<int, StaticArrayInitialization>.Empty;
                    var scalarBoxOperands = ImmutableDictionary.CreateBuilder<int, PrimitiveTypeCode>();
                    var boxedTypeTestOperands = ImmutableHashSet.CreateBuilder<int>();
                    var nullableUnboxOperands = ImmutableHashSet.CreateBuilder<int>();
                    var runtimeTokens = ImmutableDictionary.CreateBuilder<int, RuntimeTypeToken>();
                    var calls = ImmutableDictionary.CreateBuilder<int, IrMethodId>();
                    var fields = ImmutableDictionary.CreateBuilder<int, ObjectFieldId>();
                    var types = ImmutableDictionary.CreateBuilder<int, ObjectTypeId>();
                    var callable = ImmutableDictionary.CreateBuilder<int, ObjectTypeId>();
                    var vectors = ImmutableDictionary.CreateBuilder<int, ObjectTypeId>();
                    var intrinsics = ImmutableDictionary.CreateBuilder<int, ValueIntrinsicCall>();
                    var literals = ImmutableDictionary.CreateBuilder<int, ImmutableArray<ushort>>();
                    var interfaceCalls = ImmutableDictionary.CreateBuilder<int, ObjectInterfaceDeclaration>();
                    var abstractCalls = ImmutableDictionary.CreateBuilder<int, ObjectAbstractMethodDeclaration>();
                    var memory = ImmutableDictionary.CreateBuilder<int, IrType>();
                    var defaultStatics = ImmutableDictionary.CreateBuilder<int, IrType>();
                    var initializers = ImmutableDictionary.CreateBuilder<int, IrMethodId>();
                    var staticFields = ImmutableDictionary.CreateBuilder<int, ObjectStaticField>();
                    var narrowFields = ImmutableDictionary.CreateBuilder<int, IntegerCastTarget>();
                    var referenceBoxes = ImmutableDictionary.CreateBuilder<int, IrType>();
                    var constrained = ConstrainedPrefixes(work);
                    var constrainedCalls = ImmutableDictionary.CreateBuilder<int, ConstrainedValueCall>();
                    var constrainedReferences = ImmutableDictionary.CreateBuilder<int, ConstrainedReferenceCall>();
                    var boxedRenderCalls = ImmutableDictionary.CreateBuilder<int, ObjectTypeId>();
                    var boxedFormatCalls = ImmutableDictionary.CreateBuilder<int, ImmutableArray<ObjectTypeId>>();
                    var factoryConstructors = ImmutableDictionary.CreateBuilder<int, ObjectTypeId>();
                    var enumComparisons = ImmutableDictionary.CreateBuilder<int, ConstrainedValueCall>();
                    var ordinalCollections = ImmutableDictionary.CreateBuilder<int, IrMethodId>();
                    var ordinalSorts = ImmutableDictionary.CreateBuilder<int, OrdinalListSortCall>();
                    var ordinalComparisons = ImmutableHashSet.CreateBuilder<int>();
                    var literalIntegerFormats = ImmutableHashSet.CreateBuilder<int>();
                    var ordinalPrefixes = ImmutableHashSet.CreateBuilder<int>();
                    var keyOperations = ImmutableDictionary.CreateBuilder<int, ClosedKeyIntrinsic>();
                    for (int instructionIndex = 0; instructionIndex < work.Input.Instructions.Length; ++instructionIndex)
                    {
                        var instruction = work.Input.Instructions[instructionIndex];
                        var offset = instruction.Offset;
                        if (staticArrays.ContainsKey(offset) || staticArrays.Values.Any(a => a.TokenOffset == offset)) continue;
                        if (integers64) CheckOperandTypeUses(work, instruction);
                        if (integers64 && instruction.OpCode == "ldtoken") runtimeTokens.Add(offset, RuntimeToken(work, instruction));
                        else if (exceptions && instruction.OpCode == "ldstr")
                        {
                            _ = IncludeString(work, offset);
                            if (_literalUnits >= ObjectStringLimits.MaximumTotalLiteralCodeUnits) Fail(work, "String literal budget exceeded.", offset, DiagnosticIds.TranslationLimit);
                            var literal = Require(resolver.ReadUserString(work.Assembly, instruction.IntegerOperand ?? 0,
                                Math.Min(ObjectStringLimits.MaximumLiteralCodeUnits, ObjectStringLimits.MaximumTotalLiteralCodeUnits - _literalUnits)), work, offset);
                            _literalUnits += literal.CodeUnits.Length; literals.Add(offset, literal.CodeUnits);
                        }
                        else if (exceptions && instruction.OpCode is ("isinst" or "castclass"))
                        {
                            var signature = Require(resolver.ResolveClosedTypeOperand(work.Resolved, MetadataTokens.EntityHandle(Token(work, instruction, 0x01, 0x02, 0x1b))), work, offset);
                            if (integers64 && BoxedTypeTestOperand(signature, work, offset) is { } boxedTest)
                            {
                                types.Add(offset, boxedTest); boxedTypeTestOperands.Add(offset);
                                continue;
                            }
                            types.Add(offset, ReferenceOperand(signature, work, offset));
                        }
                        else if (arrays && instruction.OpCode == "newarr")
                        {
                            if (integers64)
                            {
                                var signature = Require(resolver.ResolveClosedTypeOperand(work.Resolved, MetadataTokens.EntityHandle(Token(work, instruction, 0x01, 0x02, 0x1b))), work, offset);
                                var vector = Vector(signature, work, offset);
                                RecordGenericArray(work, instruction, vector, empty: false);
                                vectors.Add(offset, vector); continue;
                            }
                            int token = Token(work, instruction, 0x01, 0x02);
                            var key = (work.Assembly.ContentHash, token);
                            if (!_types.TryGetValue(key, out var element))
                            {
                                element = Require(resolver.ResolveType(work.Assembly, MetadataTokens.EntityHandle(token)), work, offset);
                                _types.Add(key, element);
                            }
                            _integer ??= ObjectModelBuilder.Id(Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Int32), work, offset));
                            if (ObjectModelBuilder.Id(element) != _integer)
                                Fail(work, "Value CIL array allocation requires the selected runtime's Int32 element; reference/value arrays require later translation.", offset);
                            vectors.Add(offset, IntegerArray());
                        }
                        else if (arrays && (instruction.OpCode is ("ldelem.i4" or "stelem.i4") || !integers64 && instruction.OpCode == "ldlen")) _ = IntegerArray();
                        else if (integers64 && instruction.OpCode is ("ldelem.i8" or "stelem.i8")) _ = Vector(new TypeSignature.Primitive(PrimitiveTypeCode.Int64), work, offset);
                        else if (integers64 && instruction.OpCode is ("ldelem.u1" or "stelem.i1")) _ = Vector(new TypeSignature.Primitive(PrimitiveTypeCode.Byte), work, offset);
                        else if (integers64 && instruction.OpCode is ("ldelem.u2" or "stelem.i2")) _ = Vector(new TypeSignature.Primitive(PrimitiveTypeCode.Char), work, offset);
                        else if (integers64 && instruction.OpCode is ("ldelem.ref" or "stelem.ref")) _ = Vector(new TypeSignature.Primitive(PrimitiveTypeCode.Object), work, offset);
                        else if (integers64 && instruction.OpCode is ("ldelem" or "stelem" or "ldelema"))
                        {
                            var signature = Require(resolver.ResolveClosedTypeOperand(work.Resolved, MetadataTokens.EntityHandle(Token(work, instruction, 0x01, 0x02, 0x1b))), work, offset);
                            vectors.Add(offset, Vector(signature, work, offset));
                        }
                        else if (instruction.OpCode == "call" || classes && instruction.OpCode == "newobj" || delegates && instruction.OpCode is ("ldftn" or "callvirt"))
                        {
                            int token = Token(work, instruction, 0x06, 0x0a, 0x2b);
                            var key = (work.Assembly.ContentHash, token, generics ? work.Binding.Method : null);
                            if (!_calls.TryGetValue(key, out _))
                            {
                                _calls[key] = Require(generics
                                    ? resolver.ResolveClosedCall(work.Resolved, MetadataTokens.EntityHandle(token), _metadata)
                                    : resolver.ResolveDeclaredMethod(work.Assembly, MetadataTokens.EntityHandle(token), _metadata), work, offset);
                            }
                            var target = _calls[key];
                            if (EnumComparison(work, instruction, target, constrained) is { } enumComparison)
                            {
                                enumComparisons.Add(offset, enumComparison); _arguments += 2;
                                if (_arguments > ProgramLimits.MaximumCallArguments) Fail(work, "Aggregate enum comparison argument limit exceeded.", offset, DiagnosticIds.TranslationLimit);
                                continue;
                            }
                            if (OrdinalListSort(work, instruction, ref target) is { } ordinalGetter) ordinalSorts.Add(offset, ordinalGetter);
                            if (OrdinalCollectionConstruction(work, instruction, ref target) is { } ordinalCollection)
                            {
                                ordinalCollections.Add(offset, ordinalCollection.Getter);
                                factoryConstructors.Add(offset, ordinalCollection.Owner);
                            }
                            var ordinalBridge = OrdinalTextBridge(work, instruction, ref target);
                            if (ordinalBridge == OrdinalTextBridgeKind.Prefix) ordinalPrefixes.Add(offset);
                            else if (ordinalBridge == OrdinalTextBridgeKind.Compare) ordinalComparisons.Add(offset);
                            if ((CollectionConstructor(work, instruction, ref target) ?? CharacterStringConstructor(work, instruction, ref target)) is { } constructedType) factoryConstructors.Add(offset, constructedType);
                            DictionaryKeyText(work, instruction, ref target);
                            if (BoxedFormat(work, instruction, instructionIndex, ref target, out bool literalIntegerFormat) is { } formatBox) boxedFormatCalls.Add(offset, formatBox);
                            if (literalIntegerFormat) literalIntegerFormats.Add(offset);
                            if (BoxedRender(work, instruction, ref target) is { } renderBox) boxedRenderCalls.Add(offset, renderBox);
                            ComparerOperation(work, instruction, ref target);
                            if (KeyOperation(work, instruction, ref target) is { } keyOperation)
                            {
                                _arguments += keyOperation.Operation == ClosedKeyOperation.Equal ? 2 : keyOperation.Operation == ClosedKeyOperation.ContainsReferences ? 0 : 1;
                                if (_arguments > ProgramLimits.MaximumCallArguments) Fail(work, "Aggregate key intrinsic argument limit exceeded.", offset, DiagnosticIds.TranslationLimit);
                                keyOperations.Add(offset, keyOperation); continue;
                            }
                            if (constrained.TryGetValue(offset, out var prefix))
                            {
                                var signature = Require(resolver.ResolveClosedTypeOperand(work.Resolved,
                                    MetadataTokens.EntityHandle(Token(work, prefix, 0x01, 0x02, 0x1b))), work, prefix.Offset);
                                var normalized = OperandSignature(signature, work, prefix.Offset);
                                if (normalized is TypeSignature.Named { Kind: SignatureTypeKind.Class }
                                    or TypeSignature.Generic { Definition.Kind: SignatureTypeKind.Class }
                                    or TypeSignature.Primitive { Code: PrimitiveTypeCode.String or PrimitiveTypeCode.Object }
                                    or TypeSignature.Vector)
                                {
                                    if ((target.Signature.Header & 0x20) == 0)
                                        Fail(work, "Constrained reference calls require an instance declaration.", offset);
                                    var storage = Storage(normalized, work, prefix.Offset);
                                    constrainedReferences.Add(offset, new(prefix.Offset, storage.Class!, target.Signature.Parameters.Count));
                                }
                                else
                                {
                                    var receiver = signature switch {
                                        TypeSignature.Named { Kind: SignatureTypeKind.ValueType or SignatureTypeKind.Unknown } named => new ConstructedType(named.Definition, SignatureList<TypeSignature>.Empty),
                                        TypeSignature.Generic { Definition.Kind: SignatureTypeKind.ValueType } generic => new ConstructedType(generic.Definition.Definition, generic.Arguments),
                                        _ => null };
                                    if (receiver is null) Fail(work, "Constrained direct value calls require a closed inline receiver; primitive fallbacks require later lowering.", prefix.Offset);
                                    var exact = Require(resolver.ResolveConstrainedValueTarget(target, receiver!, library, _metadata), work, offset);
                                    target = exact.Implementation;
                                    constrainedCalls.Add(offset, new(prefix.Offset, Include(receiver!, work, prefix.Offset)));
                                }
                            }
                            if (generics)
                            {
                                try { _ = ClosedGenericKey.Create(target.DeclaringType.Arguments, target.MethodArguments); }
                                catch (ArgumentException error) { Fail(work, error.Message, offset, DiagnosticIds.TranslationLimit); }
                                foreach (var argument in target.DeclaringType.Arguments.Concat(target.MethodArguments))
                                    CheckArgumentAccess(argument, work, offset);
                            }
                            if (exceptions && SortComparisonName(work, instruction, target) is { } comparisonName)
                            { literals.Add(offset, comparisonName); continue; }
                            if (exceptions && !factoryConstructors.ContainsKey(offset) && (ArrayCopyStoreCall(work, instruction, target) ?? TupleShapeCall(work, instruction, target) ?? EnumTextCall(work, instruction, target) ?? EnumQueryCall(work, instruction, target) ?? RuntimeTypeCall(work, instruction, target) ?? OrdinalStringCall(work, instruction, target) ?? EmptyArrayCall(work, instruction, target) ?? StringConstructionCall(work, instruction, target) ?? IntegerCacheCall(work, instruction, target) ?? CultureCall(work, instruction, target) ?? ExceptionCall(work, instruction, target)) is { } intrinsic)
                            {
                                _arguments += intrinsic.Parameters + (intrinsic.ExceptionMessage is null ? 0 : 3);
                                if (_arguments > ProgramLimits.MaximumCallArguments) Fail(work, "Aggregate intrinsic argument limit exceeded.", offset, DiagnosticIds.TranslationLimit);
                                if (intrinsic.Kind == ValueIntrinsic.EmptyArray) RecordGenericArray(work, instruction, intrinsic.Type, empty: true);
                                intrinsics.Add(offset, intrinsic); continue;
                            }
                            if (delegates && DelegateType(target.DeclaringType, work, offset) is { } declaration)
                            {
                                var facts = _delegateFacts[ObjectTypeId.From(declaration.Source)];
                                if (!(instruction.OpCode == "newobj" && target.Handle == facts.Constructor
                                    || instruction.OpCode == "callvirt" && target.Handle == facts.Invoke))
                                    Fail(work, "Delegate members require paired ldftn/newobj construction or callvirt Invoke; multicast and other members are unsupported.", offset);
                                // Int32 is also the lowered carrier for Boolean and narrow integers.
                                // Authenticate declared scalar identity before that distinction is erased.
                                if (instruction.OpCode == "newobj" && instructionIndex > 0
                                    && work.Input.Instructions[instructionIndex - 1] is { OpCode: "ldftn" } pointer
                                    && calls.TryGetValue(pointer.Offset, out var captured))
                                {
                                    var signature = _methods[captured].Input.Signature;
                                    bool scalarResult = facts.Signature.ReturnType is TypeSignature.Primitive {
                                        Code: PrimitiveTypeCode.Int32 or PrimitiveTypeCode.Int64 or PrimitiveTypeCode.Boolean or PrimitiveTypeCode.Void };
                                    if (scalarResult && signature.ReturnType != facts.Signature.ReturnType
                                        || signature.Parameters.Count != facts.Signature.Parameters.Count
                                        || facts.Signature.Parameters.Zip(signature.Parameters).Any(pair =>
                                            pair.First is TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 or PrimitiveTypeCode.Int64 } && pair.First != pair.Second))
                                        Fail(work, "Delegate scalar target signature must match exact declared parameter and return types before lowering.", offset);
                                }
                                _arguments += instruction.OpCode == "newobj" ? 1 : declaration.ParameterCount + 1;
                                if (_arguments > ProgramLimits.MaximumCallArguments) Fail(work, "Aggregate delegate argument limit exceeded.", offset, DiagnosticIds.TranslationLimit);
                                callable.Add(offset, IncludeReference(target.DeclaringType, work, offset));
                                continue;
                            }
                            if (instruction.OpCode == "callvirt" && !integers64) Fail(work, "Only delegate Invoke is supported by this callvirt profile.", offset);
                            if (integers64 && instruction.OpCode == "callvirt" && !constrainedCalls.ContainsKey(offset) && (SnapshotClosed(target.DeclaringType, work, offset).Attributes & TypeAttributes.Interface) != 0)
                            {
                                _interfaceSession ??= new(resolver, library, _metadata, closed: true,
                                    signature: (type, allowVoid) => Storage(type, root, 0, borrow: !allowVoid, allowVoid: allowVoid));
                                ObjectInterfaceDeclaration interfaceDeclaration;
                                try { interfaceDeclaration = _interfaceSession.Declare(target); }
                                catch (ObjectInterfacePlanBuilder.PlanFailure failure) { throw new Failure(failure.Diagnostic with { Location = Location(work, offset) }); }
                                _interfaces.TryAdd(IrMethodId.From(interfaceDeclaration.Source), (target, work, offset));
                                _arguments += interfaceDeclaration.Parameters.Length;
                                if (_arguments > ProgramLimits.MaximumCallArguments) Fail(work, "Aggregate interface argument limit exceeded.", offset, DiagnosticIds.TranslationLimit);
                                interfaceCalls.Add(offset, interfaceDeclaration); continue;
                            }
                            if (target.MethodArguments.Count != 0 && (target.Signature.Header & 0x20) != 0)
                                Fail(work, "Generic instance methods require an exact interface call; direct/class calls and method pointers need later lowering.", offset);
                            target = NarrowVirtualReceiver(work, instruction, target);
                            if (integers64 && instruction.OpCode == "callvirt" && !constrainedCalls.ContainsKey(offset)
                                && AbstractDeclaration(target, work, offset) is { } abstractCall)
                            {
                                abstractCalls.Add(offset, abstractCall);
                                _arguments += abstractCall.Parameters.Length;
                                if (_arguments > ProgramLimits.MaximumCallArguments) Fail(work, "Aggregate abstract-call argument limit exceeded.", offset, DiagnosticIds.TranslationLimit);
                                _virtualCalls.TryAdd(IrMethodId.From(abstractCall.Source), (work, offset));
                                continue;
                            }
                            var callee = Discover(target, work, offset);
                            if (instruction.OpCode == "callvirt" && callee.Binding.Kind != (constrainedCalls.ContainsKey(offset) ? ObjectMethodKind.ValueInstance : ObjectMethodKind.Instance))
                                Fail(work, "callvirt requires a class instance method.", offset);
                            if (instruction.OpCode == "callvirt" && callee.Binding.VirtualSlot is not null
                                && (callee.Binding.Attributes & MethodAttributes.Final) == 0)
                                _virtualCalls.TryAdd(callee.Binding.Method, (work, offset));
                            bool factoryConstructor = factoryConstructors.ContainsKey(offset);
                            if (factoryConstructor && (callee.Binding.Kind != ObjectMethodKind.Static || callee.Binding.Owner != factoryConstructors[offset]
                                || (callee.Parameters.Length != 1 && !(ordinalCollections.ContainsKey(offset) && callee.Parameters.Length == 2 && callee.Parameters[0] == IrType.Int32))
                                || callee.ReturnType != IrType.Reference(factoryConstructors[offset])))
                                Fail(work, "Constructor factory requires its exact checked static signature.", offset);
                            if (instruction.OpCode == "newobj" && !factoryConstructor && (SnapshotClosed(target.DeclaringType, work, offset).Attributes & TypeAttributes.Abstract) != 0)
                                Fail(work, "Cannot allocate an abstract class.", offset);
                            if (instruction.OpCode == "newobj" && !factoryConstructor && callee.Binding.Kind is not (ObjectMethodKind.Constructor or ObjectMethodKind.ObjectBaseConstructor or ObjectMethodKind.ValueConstructor))
                                Fail(work, "newobj requires an exact supported constructor declaration.", offset);
                            if (!ObjectMethodAccess.Allowed(work.Binding, callee.Binding, integers64)
                                && !(instruction.OpCode == "call" && (ProtectedBaseConstructor(work, callee, offset) || ProtectedStaticCall(work, callee, offset))))
                                Fail(work, "Calls require the same declaring owner, an accessible public/assembly target, a private target in a proven nongeneric containing type, or a protected static target on a proven base class under the integer-object profile.", offset);
                            _arguments += instruction.OpCode == "ldftn" ? 0 : callee.Parameters.Length - (instruction.OpCode == "newobj" && !factoryConstructor ? 1 : 0) + (ordinalPrefixes.Contains(offset) || ordinalComparisons.Contains(offset) || ordinalSorts.ContainsKey(offset) ? 1 : 0);
                            if (_arguments > ProgramLimits.MaximumCallArguments) Fail(work, "Aggregate call-argument limit exceeded.", offset, DiagnosticIds.TranslationLimit);
                            calls.Add(offset, callee.Binding.Method);
                        }
                        else if (integers64 && instruction.OpCode is ("ldsfld" or "stsfld"))
                        {
                            int token = Token(work, instruction, 0x04, 0x0a);
                            var field = BoundField(work, token, offset);
                            if (IntegerCasts.NarrowStorage(field.Signature) is { } staticWidth) narrowFields.Add(offset, staticWidth);
                            var owner = SnapshotClosed(field.DeclaringType, work, offset);
                            var declaration = owner.Fields.Single(f => f.Token == MetadataTokens.GetToken(field.Handle));
                            if (EmptyStringField(work, instruction, field, owner, declaration))
                            { literals.Add(offset, []); continue; }
                            if (instruction.OpCode == "stsfld" || !DefaultStaticFields.IsDefault(owner, declaration))
                            {
                                staticFields.Add(offset, StaticField(work, field, owner, declaration, instruction.OpCode == "stsfld", offset));
                                if (owner.HasTypeInitializer) initializers.Add(offset, FieldInitializer(field.DeclaringType, work, offset));
                                continue;
                            }
                            var access = declaration.Attributes & FieldAttributes.FieldAccessMask;
                            if (access == FieldAttributes.PrivateScope || work.Binding.Owner != ClosedId(field.DeclaringType, work, offset)
                                && (access != FieldAttributes.Public || !owner.ExternallyVisible && !ObjectLexicalAccess.Visible(work.Binding, ClosedId(field.DeclaringType, work, offset), owner.LexicalOwners)))
                                Fail(work, "Default static fields require the same owner or a public member on an externally visible type.", offset);
                            defaultStatics.Add(offset, Storage(field.Signature, work, offset));
                        }
                        else if (instruction.OpCode is "ldfld" or "ldflda" or "stfld")
                        {
                            int token = Token(work, instruction, 0x04, 0x0a);
                            var field = BoundField(work, token, offset);
                            if (integers64 && IntegerCasts.NarrowStorage(field.Signature) is { } fieldWidth)
                            {
                                if (instruction.OpCode == "ldflda") Fail(work, "Narrow field addresses require storage-width-aware locations before execution.", offset);
                                narrowFields.Add(offset, fieldWidth);
                            }
                            if (!generics && field.DeclaringType.Arguments.Count != 0) Fail(work, "Generic field owners require later lowering.", offset);
                            var owner = field.DeclaringType.Definition;
                            fields.Add(offset, new(classes && !Inline(owner, work, offset) ? IncludeReference(field.DeclaringType, work, offset)
                                : Include(field.DeclaringType, work, offset), MetadataTokens.GetToken(field.Handle)));
                        }
                        else if (instruction.OpCode is "initobj" or "ldobj" or "stobj" or "cpobj"
                            || boxing && instruction.OpCode is ("box" or "unbox" or "unbox.any"))
                        {
                            int token = generics ? Token(work, instruction, 0x01, 0x02, 0x1b) : Token(work, instruction, 0x01, 0x02);
                            if (generics)
                            {
                                var signature = Require(resolver.ResolveClosedTypeOperand(work.Resolved, MetadataTokens.EntityHandle(token)), work, offset);
                                if (instruction.OpCode is "box" or "unbox" or "unbox.any" && IsNullable(signature, work, offset))
                                {
                                    calls.Add(offset, NullableBoxCall(signature, instruction.OpCode, work, offset));
                                    continue;
                                }
                                if (integers64 && instruction.OpCode is "initobj" or "ldobj" or "stobj" or "cpobj")
                                {
                                    signature = OperandSignature(signature, work, offset);
                                    // OperandSignature validates nominal enum access against the calling assembly/scope.
                                    // Keep the stricter generic-argument rule for other memory operand shapes.
                                    if (!EnumScalar(signature, work, offset)) CheckArgumentAccess(signature, work, offset);
                                    if (IntegerCasts.NarrowStorage(signature) is not null)
                                        Fail(work, "Narrow memory operands require storage-width-aware locations before execution.", offset);
                                    memory.Add(offset, Storage(signature, work, offset));
                                    continue;
                                }
                                if (integers64 && boxing)
                                {
                                    var normalized = OperandSignature(signature, work, offset);
                                    bool referenceOperand = normalized is TypeSignature.Primitive { Code: PrimitiveTypeCode.Object or PrimitiveTypeCode.String }
                                        or TypeSignature.Named { Kind: SignatureTypeKind.Class }
                                        or TypeSignature.Generic { Definition.Kind: SignatureTypeKind.Class } or TypeSignature.Vector;
                                    if (referenceOperand)
                                    {
                                        if (instruction.OpCode == "unbox") Fail(work, "unbox requires a boxed value type; reference operands support box identity or unbox.any checked casts.", offset);
                                        CheckArgumentAccess(normalized, work, offset);
                                        referenceBoxes.Add(offset, Storage(normalized, work, offset));
                                        continue;
                                    }
                                }
                                if (integers64 && ScalarBoxOperand(signature, work, offset) is { } scalarOperand)
                                {
                                    if (instruction.OpCode == "unbox") Fail(work, "Raw scalar unbox requires width-aware borrowed locations; use supported unbox.any copies.", offset);
                                    types.Add(offset, scalarOperand.Type); scalarBoxOperands.Add(offset, scalarOperand.Code);
                                    if (instruction.OpCode == "unbox.any" && NullableUnboxHelper(work, signature, offset)) nullableUnboxOperands.Add(offset);
                                    continue;
                                }
                                if (instruction.OpCode == "unbox.any" && NullableUnboxHelper(work, signature, offset))
                                    nullableUnboxOperands.Add(offset);
                                var operandType = signature switch
                                {
                                    TypeSignature.Named { Kind: SignatureTypeKind.ValueType or SignatureTypeKind.Unknown } named => new ConstructedType(named.Definition, SignatureList<TypeSignature>.Empty),
                                    TypeSignature.Generic { Definition.Kind: SignatureTypeKind.ValueType } generic => new ConstructedType(generic.Definition.Definition, generic.Arguments),
                                    _ => null,
                                };
                                if (operandType is null) Fail(work, "This closed generic type operand requires an inline struct; primitive/reference operand lowering remains unsupported.", offset);
                                types.Add(offset, Include(operandType!, work, offset));
                                continue;
                            }
                            var key = (work.Assembly.ContentHash, token);
                            if (!_types.TryGetValue(key, out var type))
                            {
                                type = Require(resolver.ResolveType(work.Assembly, MetadataTokens.EntityHandle(token)), work, offset);
                                _types.Add(key, type);
                            }
                            types.Add(offset, Include(type, work, offset));
                        }
                    }
                    methods.Add(new(work.Input, work.Binding, work.Parameters, work.ReturnType, work.Locals,
                        calls.ToImmutable(), fields.ToImmutable(), types.ToImmutable()) { LiteralIntegerFormats = literalIntegerFormats.ToImmutable(), OrdinalCollectionGetters = ordinalCollections.ToImmutable(), OrdinalCompareCalls = ordinalComparisons.ToImmutable(), EnumCompareCalls = enumComparisons.ToImmutable(), OrdinalSortCalls = ordinalSorts.ToImmutable(), StaticArrays = staticArrays, OrdinalPrefixCalls = ordinalPrefixes.ToImmutable(), FactoryConstructorCalls = factoryConstructors.ToImmutable(), GenericArrays = GenericArrays(work), BoxedTypeTestOperands = boxedTypeTestOperands.ToImmutable(), ScalarBoxOperands = scalarBoxOperands.ToImmutable(), NullableUnboxOperands = nullableUnboxOperands.ToImmutable(), RuntimeTypeTokens = runtimeTokens.ToImmutable(), NarrowFields = narrowFields.ToImmutable(), BoxedRenderCalls = boxedRenderCalls.ToImmutable(), BoxedFormatCalls = boxedFormatCalls.ToImmutable(), Initializers = initializers.ToImmutable(), StaticFields = staticFields.ToImmutable(), KeyOperations = keyOperations.ToImmutable(), DefaultStatics = defaultStatics.ToImmutable(), ConstrainedReferenceCalls = constrainedReferences.ToImmutable(), ConstrainedCalls = constrainedCalls.ToImmutable(), ReferenceBoxOperands = referenceBoxes.ToImmutable(), MemoryOperands = memory.ToImmutable(), InterfaceOperands = interfaceCalls.ToImmutable(), AbstractOperands = abstractCalls.ToImmutable(), ArrayOperands = vectors.ToImmutable(), DelegateOperands = callable.ToImmutable(), ExceptionRegions = work.Regions, Intrinsics = intrinsics.ToImmutable(), Literals = literals.ToImmutable() });
                }
                if (_interfaceSession is not null)
                {
                    foreach (var reference in _interfaceSession.ClosedRoots) IncludeReference(reference, root, 0);
                    foreach (var vector in _interfaceSession.Arrays) _vectors.Add(vector);
                }
                if (!generics && _values.Count == 0) Fail(root, "Value discovery requires an inline value; use the existing primitive/object translator for other programs.");
                var built = generics ? ValueModelBuilder.BuildClosedCatalog(resolver, [.. _values.Values], library, _metadata, _integerArray is not null,
                    [.. _references.Values], _snapshots, delegates ? type => {
                        if (exceptions) IncludeExceptionHierarchy(type, root, 0);
                        return DelegateType(type, root, 0);
                    } : null, integers64, [.. _vectors], (type, input) => {
                        var id = ObjectModelBuilder.Id(type); _knownTypes.TryAdd(id, type);
                        if (_snapshots.TryAdd(id, input)) _snapshotFields += input.Fields.Length;
                    }, _scalarBoxes, _enumMetadataRequired ? type => EnumConstants(type, root) : null, _enumArrays) : ValueModelBuilder.BuildCatalog(resolver, [.. _values.Values.Select(t => t.Definition)], library, _metadata, _integerArray is not null,
                    [.. _references.Values.Select(t => t.Definition)], _snapshots);
                catalog = built.Catalog ?? throw new Failure(built.Diagnostics[0]);
                catalog = catalog with { References = catalog.References with { Statics = [.. _staticFields.Values.OrderBy(f => f.Id.Owner.ContentHash).ThenBy(f => f.Id.Owner.Token).ThenBy(f => f.Id.Owner.Specialization).ThenBy(f => f.Id.Token)] } };
                if (_interfaceSession is not null)
                {
                    try { interfacePlan = _interfaceSession.RunPrepared(catalog.References, _knownTypes, [.. _interfaces.Values.Select(s => s.Method)], catalog); }
                    catch (ObjectInterfacePlanBuilder.PlanFailure failure)
                    {
                        if (failure.Declaration is { } id && _interfaces.TryGetValue(id, out var site))
                            throw new Failure(failure.Diagnostic with { Message = failure.Diagnostic.Message + Path(site.Caller), Location = Location(site.Caller, site.Offset) });
                        throw new Failure(failure.Diagnostic);
                    }
                    if (_methods.Keys.Concat(_abstractMethods.Keys).Concat(interfacePlan.Declarations.Select(d => IrMethodId.From(d.Source)))
                        .Concat(interfacePlan.Methods.Select(m => m.Binding.Method)).Distinct().Count() + _exceptionDeclarations.Count > ProgramLimits.MaximumMethods)
                        Fail(root, "Bodies, interface declarations and mapping facts share the method limit.", code: DiagnosticIds.TranslationLimit);
                    var facts = interfacePlan.Methods.ToDictionary(m => m.Binding.Method);
                    foreach (var map in interfacePlan.Dispatch)
                    {
                        var site = _interfaces[map.Declaration];
                        foreach (var target in map.Targets)
                            Discover(_interfaceSession.Resolved(target.Implementation), site.Caller, site.Offset, facts[target.Implementation]);
                    }
                }
                var relations = new IrTypeRules(catalog.References);
                int interfaceTargets = interfacePlan?.Dispatch.Sum(m => m.Targets.Length) ?? 0;
                if (_dispatchTargetCount + interfaceTargets > ObjectDispatchLimits.MaximumTargets)
                    Fail(root, "Aggregate class/interface dispatch target limit exceeded.", code: DiagnosticIds.TranslationLimit);
                foreach (var site in _virtualCalls.ToArray())
                {
                    var abstractTarget = _abstractMethods.GetValueOrDefault(site.Key);
                    var declarationBinding = abstractTarget?.Binding ?? _methods[site.Key].Binding;
                    var declarationMethod = abstractTarget?.Resolved ?? _methods[site.Key].Resolved;
                    if (!_dispatch.TryGetValue(site.Key, out var targets)) _dispatch.Add(site.Key, targets = []);
                    foreach (var layout in catalog.References.Types)
                    {
                        var receiver = ObjectTypeId.From(layout.Source);
                        if ((layout.Attributes & TypeAttributes.Abstract) != 0) continue;
                        if (!relations.Assignable(IrType.Reference(receiver), IrType.Reference(declarationBinding.Owner)) || targets.ContainsKey(receiver)) continue;
                        if (_dispatchTargetCount + interfaceTargets >= ObjectDispatchLimits.MaximumTargets)
                            Fail(site.Value.Caller, "Aggregate class/interface dispatch target limit exceeded.", site.Value.Offset, DiagnosticIds.TranslationLimit);
                        var exact = ResolveVirtual(declarationMethod, _knownTypes[receiver], site.Value.Caller, site.Value.Offset);
                        var implementation = Discover(exact.Implementation, site.Value.Caller, site.Value.Offset);
                        targets.Add(receiver, implementation.Binding.Method); _dispatchTargetCount++;
                    }
                }
                if (_pending.Count == 0) break;
            }
            var layouts = catalog.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
            var declarations = catalog.Types.SelectMany(t => t.Fields).Concat(catalog.References.Types.SelectMany(t => t.Fields)).ToDictionary(f => f.Id);
            foreach (var method in methods)
            {
                var work = _methods[method.Binding.Method];
                var writes = work.Input.Instructions.Where(i => i.OpCode == "stfld").Select(i => i.Offset).ToHashSet();
                foreach (var access in method.Fields)
                {
                    var field = declarations[access.Value];
                    bool visible = layouts.TryGetValue(field.Id.Owner, out var inlineOwner)
                        ? inlineOwner.ExternallyVisible || integers64 && SourceTypeVisible(method.Binding, field.Id.Owner, _snapshots[field.Id.Owner].LexicalOwners)
                        : _snapshots[field.Id.Owner].ExternallyVisible
                        || integers64 && ObjectLexicalAccess.Visible(method.Binding, field.Id.Owner, _snapshots[field.Id.Owner].LexicalOwners);
                    if ((field.Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.PrivateScope
                        || method.Binding.Owner != field.Id.Owner
                        && ((field.Attributes & FieldAttributes.FieldAccessMask) != FieldAttributes.Public || !visible))
                        Fail(work, "Fields require the same declaring owner or a public member on an externally visible type.", access.Key);
                    // Taking an address retains readonly capability in body translation.
                    if ((field.Attributes & FieldAttributes.InitOnly) != 0
                        && writes.Contains(access.Key) && !(method.Binding.Kind is (ObjectMethodKind.Constructor or ObjectMethodKind.ValueConstructor) && method.Binding.Owner == field.Id.Owner))
                        Fail(work, classes ? "Readonly fields require the declaring class constructor and its checked receiver."
                            : "Readonly value fields cannot be written by this static-method profile.", access.Key);
                    if (field.Storage is ObjectStorage.Value inline) CheckType(inline.Type, access.Key);
                }
                foreach (var type in method.Parameters.Concat(method.Locals).Append(method.ReturnType).Concat(method.MemoryOperands.Values).Concat(method.ReferenceBoxOperands.Values))
                {
                    var element = type.Kind == IrTypeKind.ManagedLocation ? type.Element! : type;
                    if (element.Kind == IrTypeKind.Value) CheckType(element.Class!, 0);
                    else if (classes && element.Kind == IrTypeKind.Reference && element.Class!.ArrayElement is null)
                    {
                        if (!integers64 && _snapshots.TryGetValue(element.Class, out var reference) && !reference.ExternallyVisible && element.Class != method.Binding.Owner
                            && (!integers64 || !ObjectLexicalAccess.Visible(method.Binding, element.Class, reference.LexicalOwners)))
                            Fail(work, "Class type use requires the same declaring owner or a fully externally visible type.");
                        if (generics && _references.TryGetValue(element.Class, out var constructed))
                            foreach (var argument in constructed.Arguments) CheckArgumentAccess(argument, work, 0);
                    }
                }
                foreach (var operand in method.TypeOperands)
                    if (layouts.ContainsKey(operand.Value)) CheckType(operand.Value, operand.Key);

                void CheckType(ObjectTypeId type, int offset)
                {
                    if (generics && _values.TryGetValue(type, out var constructed))
                        foreach (var argument in constructed.Arguments) CheckArgumentAccess(argument, work, offset);
                    if (type != method.Binding.Owner && !layouts[type].ExternallyVisible)
                    {
                        if (integers64 && layouts[type].ScalarEnum)
                        {
                            var definition = _values[type].Definition;
                            if (!OpaqueValueArgument(definition, work, offset)) CheckEnumDeclarationAccess(definition, work, offset);
                        }
                        else if (integers64) CheckValueDeclarationAccess(_values[type].Definition, work, offset);
                        else Fail(work, "Value type use requires the same declaring owner or a fully externally visible type; other visibility rules require later lowering.", offset);
                    }
                }
            }
            return new(root.Binding.Method, [.. methods.OrderBy(m => m.Binding.Method.ContentHash, StringComparer.Ordinal).ThenBy(m => m.Binding.Method.Token).ThenBy(m => m.Binding.Method.Specialization, StringComparer.Ordinal)], catalog)
                { InterfaceDispatch = interfacePlan, AbstractMethods = [.. _abstractMethods.Values.Select(a => a.Declaration).OrderBy(a => a.Source.ContentHash, StringComparer.Ordinal).ThenBy(a => a.Source.Token).ThenBy(a => a.Source.Specialization, StringComparer.Ordinal)], AbstractBindings = [.. _abstractMethods.Values.Select(a => a.Binding)],
                    Dispatch = [.. _dispatch.OrderBy(p => p.Key.ContentHash, StringComparer.Ordinal).ThenBy(p => p.Key.Token).ThenBy(p => p.Key.Specialization, StringComparer.Ordinal)
                        .Select(p => new ObjectVirtualDispatch(p.Key, [.. p.Value.OrderBy(t => t.Key.ContentHash, StringComparer.Ordinal).ThenBy(t => t.Key.Token).ThenBy(t => t.Key.Specialization, StringComparer.Ordinal)
                            .Select(t => new ObjectVirtualTarget(t.Key, t.Value))]))],
                    Exceptions = exceptions ? new(_exceptionBase!, [.. catalog.References.Types.Where(t => IsException(ObjectTypeId.From(t.Source))).Select(t => ObjectTypeId.From(t.Source))], [.. _runtimeFaults]) { TypeInitialization = _typeInitializationException, ArgumentBase = _argumentBase, Resources = [.. _exceptionResources] } : null };
        }

        private Work Discover(ResolvedMethod resolved, Work? parent, int offset, ObjectDispatchMethod? fact = null)
        {
            var owner = resolved.DeclaringType.Definition;
            if (owner.Assembly == library && owner == resolver.ResolveType(library, "Cilica.Runtime", "UnsupportedLibraryMember`1").Value)
                Fail(parent, "This library member has no implemented managed body in the selected bounded core.", offset);
            string specialization = "";
            if (generics)
            {
                try { specialization = ClosedGenericKey.Create(resolved.DeclaringType.Arguments, resolved.MethodArguments); }
                catch (ArgumentException error) { Fail(parent, error.Message, offset, DiagnosticIds.TranslationLimit); }
            }
            var id = new IrMethodId(owner.Assembly.ContentHash, MetadataTokens.GetToken(resolved.Handle), specialization);
            // Check instantiation before the definition-key cache: a generic call must never alias an ordinary one.
            if (!generics && (resolved.DeclaringType.Arguments.Count != 0 || resolved.MethodArguments.Count != 0))
                Fail(parent, "Generic method/type instantiations require later lowering.", offset);
            fact ??= _interfaceSession?.Fact(id);
            if (_methods.TryGetValue(id, out var known))
            {
                if (fact is not null && (!ObjectLexicalAccess.SameBinding(known.Binding, fact.Binding) || known.ReturnType != fact.ReturnType || !known.Parameters.SequenceEqual(fact.Parameters)))
                    Fail(parent, "Executable method contradicts its interface mapping facts.", offset);
                return known;
            }
            if (_methods.Count + _abstractMethods.Count + _exceptionDeclarations.Count >= ProgramLimits.MaximumMethods || _bytes >= ProgramLimits.MaximumBodyBytes
                || _instructions >= ProgramLimits.MaximumInstructions || _metadata.Remaining == 0)
                Fail(parent, $"Aggregate discovery limit exceeded: {_methods.Count + _abstractMethods.Count + _exceptionDeclarations.Count}/{ProgramLimits.MaximumMethods} method declarations, {_bytes}/{ProgramLimits.MaximumBodyBytes} IL bytes, {_instructions}/{ProgramLimits.MaximumInstructions} instructions, {_metadata.Remaining} metadata work units remaining.", offset, DiagnosticIds.TranslationLimit);
            var readLimits = limits with
            {
                MaximumBodyBytes = Math.Min(limits.MaximumBodyBytes, ProgramLimits.MaximumBodyBytes - _bytes),
                MaximumInstructions = Math.Min(limits.MaximumInstructions, ProgramLimits.MaximumInstructions - _instructions),
                MaximumInitializerMembers = Math.Min(limits.MaximumInitializerMembers, _metadata.Remaining),
            };
            ManagedExceptionMethodInput? protectedInput = exceptions ? Require(resolver.ReadManagedExceptionMethodInput(resolved, library, readLimits, _metadata), parent, offset) : null;
            var input = protectedInput?.Method ?? Require(generics ? resolver.ReadClosedMethodInput(resolved, library, readLimits)
                : resolver.ReadMethodInput(owner.Assembly, resolved.Handle, readLimits), parent, offset);
            if (protectedInput is not null)
                foreach (var call in protectedInput.ResolvedCalls)
                    _calls.TryAdd((owner.Assembly.ContentHash, call.Key, generics ? id : null), call.Value);
            _bytes += input.CodeSize; _instructions += input.Instructions.Length;
            // The protected reader has already charged the shared ledger, including
            // body snapshots and call-shape resolution. Other readers report a cost.
            if (!exceptions && !_metadata.TryConsume(input.MetadataMembersRead)) Fail(parent, "Aggregate metadata-member limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            bool initializer = integers64 && input.DeclaredName == ".cctor";
            bool constructor = classes && input.DeclaredName == ".ctor";
            bool instance = constructor || delegates && (input.Attributes & MethodAttributes.Static) == 0;
            var allowed = MethodAttributes.MemberAccessMask | MethodAttributes.Static | MethodAttributes.HideBySig
                | (integers64 ? MethodAttributes.SpecialName : 0)
                | (integers64 || fact is not null ? MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Final : 0)
                | (constructor || initializer ? MethodAttributes.SpecialName | MethodAttributes.RTSpecialName : 0);
            if (input.IsReferenceAssembly || !generics && (input.OwnerGenericArity != 0 || input.Signature.GenericArity != 0) || input.Signature.Header != ((instance ? 0x20 : 0) | (generics && input.Signature.GenericArity != 0 ? 0x10 : 0))
                || instance && input.Signature.GenericArity != 0 && fact is null
                || input.HasTypeInitializer && (!integers64 || (SnapshotClosed(resolved.DeclaringType, parent, offset).Attributes & TypeAttributes.BeforeFieldInit) == 0) || input.HasModuleInitializer || !exceptions && input.ExceptionRegionCount != 0
                || !input.Locals.IsEmpty && !input.InitializeLocals
                || ((input.Attributes & MethodAttributes.Static) != 0) == instance
                || (input.Attributes & ~allowed) != 0
                || (input.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.PrivateScope
                || (input.Implementation & (MethodImplAttributes.CodeTypeMask | MethodImplAttributes.ManagedMask | MethodImplAttributes.InternalCall | MethodImplAttributes.Synchronized)) != MethodImplAttributes.IL)
                Fail(parent, classes
                    ? "Value/object methods require ordinary static methods, exact class constructors or selected delegate-profile instance methods, managed IL, supported closed signatures, no strict type/module initializers and initialized locals; protected regions require the exception profile."
                    : "Value methods require static nongeneric ordinary managed IL without initialization, special names, exception regions or uninitialized locals.", offset);
            var ownerId = generics ? ClosedId(resolved.DeclaringType, parent, offset) : ObjectModelBuilder.Id(owner);
            var primitive = integers64 && instance && !constructor ? Primitive(owner, parent, offset) : null;
            bool inlineInstance = primitive is null && integers64 && instance && Inline(owner, parent, offset);
            bool inlineConstructor = inlineInstance && constructor;
            if (instance)
            {
                if (primitive is null && !inlineInstance && Inline(owner, parent, offset) || constructor && ((input.Attributes & (MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)) != (MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)
                    || input.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void }))
                    Fail(parent, "Class constructors require exact constructor flags, a class receiver and void return.", offset);
                // Scalar receivers need no nominal storage layout.
                if (primitive is null)
                {
                    if (inlineInstance) Include(resolved.DeclaringType, parent, offset);
                    else IncludeReference(resolved.DeclaringType, parent, offset);
                }
            }
            _parameters += input.Signature.Parameters.Count + (instance ? 1 : 0); _locals += input.Locals.Length;
            if (_parameters > IrLimits.MaximumParameters || _locals > IrLimits.MaximumLocals)
                Fail(parent, "Aggregate parameter/local storage limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            var kind = primitive is not null ? ObjectMethodKind.PrimitiveInstance : inlineConstructor ? ObjectMethodKind.ValueConstructor : inlineInstance ? ObjectMethodKind.ValueInstance : !instance ? ObjectMethodKind.Static : !constructor ? ObjectMethodKind.Instance : ownerId == ObjectBase() ? ObjectMethodKind.ObjectBaseConstructor : ObjectMethodKind.Constructor;
            var binding = fact?.Binding ?? new ObjectMethodBinding(id, ownerId, kind, input.Attributes, input.OwnerExternallyVisible)
                { LexicalOwners = integers64 ? input.LexicalOwners : [], OwnerAssemblyVisible = integers64 && input.OwnerAssemblyVisible, Primitive = primitive };
            if (integers64 && !instance && input.DeclaredName == "TryParse"
                && owner == Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Int32), parent, offset))
            {
                var parameters = input.Signature.Parameters;
                if (input.Attributes != (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig)
                    || input.Signature.Header != 0 || parameters.Count != 4
                    || input.Signature.ReturnType != new TypeSignature.Primitive(PrimitiveTypeCode.Boolean)
                    || parameters[0] != new TypeSignature.Primitive(PrimitiveTypeCode.String)
                    || parameters[1] is not TypeSignature.Named { Kind: SignatureTypeKind.ValueType } style
                    || style.Definition != Require(resolver.ResolveType(library, "System.Globalization", "NumberStyles"), parent, offset)
                    || parameters[2] is not TypeSignature.Named provider
                    || provider.Definition != Require(resolver.ResolveType(library, "System", "IFormatProvider"), parent, offset)
                    || parameters[3] is not TypeSignature.ByReference { Element: TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 } })
                    Fail(parent, "Int32.TryParse requires the selected String/NumberStyles/IFormatProvider/out Int32 declaration.", offset);
                _ = Require(resolver.AuthorizeLibraryIntrinsic(library, resolved.Handle, LibraryIntrinsicCapability.InvariantIntegerParsing), parent, offset);
                binding = binding with { IntegerParser = Primitive(owner, parent, offset) };
            }
            if (initializer)
            {
                if (input.Attributes != (MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)
                    || input.Signature.Parameters.Count != 0 || input.Signature.GenericArity != 0 || input.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void })
                    Fail(parent, "Type initializer requires exact private static constructor flags, no parameters and void return.", offset);
                var initializedOwner = SnapshotClosed(resolved.DeclaringType, parent, offset);
                binding = binding with { TypeInitializerName = initializedOwner.Source.Name.Replace('/', '+') };
            }
            if (primitive is not null && input.DeclaredName == "ToString")
            {
                bool defaultText = input.Signature.Parameters.Count == 0;
                if (input.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String }
                    || defaultText && input.Attributes != (MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual)
                    || !defaultText && (input.Signature.Parameters.Count != 1 || input.Signature.Parameters[0] is not TypeSignature.Named provider
                        || provider.Definition != Require(resolver.ResolveType(library, "System", "IFormatProvider"), parent, offset)))
                    Fail(parent, "Integer formatting requires the supported fixed-invariant ToString() or ToString(IFormatProvider) declaration.", offset);
                _ = Require(resolver.AuthorizeLibraryIntrinsic(library, resolved.Handle, LibraryIntrinsicCapability.InvariantIntegerFormatting), parent, offset);
                binding = binding with { InvariantProviderOnly = !defaultText };
            }
            if (fact is null && primitive is null && !inlineInstance && (input.Attributes & MethodAttributes.Virtual) != 0)
            {
                var exact = ResolveVirtual(resolved, resolved.DeclaringType, parent, offset);
                binding = binding with { VirtualName = input.DeclaredName, VirtualSlot = new(MethodId(exact.Slot), ObjectModelBuilder.Id(exact.Slot.DeclaringType)) };
            }
            var work = new Work(owner.Assembly, resolved, input, binding,
                [.. (instance ? new[] { primitive is not null ? IrType.Location(primitive.Storage) : inlineInstance ? IrType.Location(IrType.Value(ownerId)) : IrType.Reference(ownerId) } : Array.Empty<IrType>()),
                    .. input.Signature.Parameters.Select(t => Storage(t, parent, offset, borrow: true))],
                Storage(input.Signature.ReturnType, parent, offset, allowVoid: true),
                [.. input.Locals.Select(t => Storage(t, parent, offset, borrow: integers64))], parent, offset);
            if (work.Locals.Any(t => t.Kind == IrTypeKind.ManagedLocation) && (input.ExceptionRegionCount != 0
                || work.Locals.Any(t => t.Kind == IrTypeKind.ManagedLocation && t.Element!.Kind == IrTypeKind.Value)))
                Fail(parent, "Ref locals currently require scalar/reference storage in methods without exception regions.", offset);
            if (integers64)
                CheckSourceTypeUses(input.DeclarationSignature.Parameters.Prepend(input.DeclarationSignature.ReturnType)
                    .Concat(input.DeclarationLocals), work, 0);
            if (fact is not null && (binding.Kind != kind || binding.Owner != ownerId || binding.Attributes != input.Attributes
                || work.ReturnType != fact.ReturnType || !work.Parameters.SequenceEqual(fact.Parameters)))
                Fail(parent, "Closed method signature contradicts its interface mapping facts.", offset);
            if (instance && !inlineConstructor && !integers64 && work.Parameters.Concat(work.Locals).Append(work.ReturnType).Any(t => t.Kind is IrTypeKind.Value or IrTypeKind.ManagedLocation))
                Fail(parent, "Class instance methods require scalar/reference parameters, locals and returns; values and borrows need later lowering.", offset);
            if (integers64 && protectedInput is not null)
                foreach (var region in protectedInput.Regions)
                    if (region.DeclarationCatchToken is { } catchToken) CheckTypeUseToken(work, catchToken, region.HandlerStart);
            if (protectedInput is not null) work = work with { Regions = [.. protectedInput.Regions.Select(r => new IrExceptionRegion(r.TryStart, r.TryEnd, r.HandlerStart, r.HandlerEnd,
                r.Kind switch { ExceptionRegionKind.Catch => IrExceptionClause.Catch, ExceptionRegionKind.Filter => IrExceptionClause.Filter, ExceptionRegionKind.Finally => IrExceptionClause.Finally, _ => IrExceptionClause.Fault },
                r.CatchType is null ? null : ReferenceOperand(r.CatchType, work, r.HandlerStart), r.FilterStart))] };
            _methods.Add(id, work); _pending.Enqueue(work); return work;
        }

        private static IrMethodId MethodId(ResolvedMethod method) => new(method.DeclaringType.Definition.Assembly.ContentHash,
            MetadataTokens.GetToken(method.Handle), ClosedGenericKey.Create(method.DeclaringType.Arguments, method.MethodArguments));

        private ClosedClassVirtualTarget ResolveVirtual(ResolvedMethod declaration, ConstructedType receiver, Work? caller, int offset)
        {
            var key = (MethodId(declaration), ObjectModelBuilder.Id(receiver));
            if (_virtualCache.TryGetValue(key, out var known)) return known;
            var exact = Require(resolver.ResolveClosedClassVirtualTarget(declaration, receiver, library, _metadata), caller, offset);
            _virtualCache.Add(key, exact); return exact;
        }

        private TypeSignature OperandSignature(TypeSignature signature, Work work, int offset)
        {
            if (signature is not TypeSignature.Named named) return signature;
            if (EnumScalar(signature, work, offset))
            {
                if (!OpaqueValueArgument(named.Definition, work, offset)) CheckEnumDeclarationAccess(named.Definition, work, offset);
                return named with { Kind = SignatureTypeKind.ValueType };
            }
            // A TypeDef/TypeRef operand encodes selected primitives as named types.
            // Compare resolver-owned identities; a matching spelling is insufficient.
            foreach (var code in new[] { PrimitiveTypeCode.Int32, PrimitiveTypeCode.UInt32, PrimitiveTypeCode.Int64, PrimitiveTypeCode.UInt64,
                PrimitiveTypeCode.Boolean, PrimitiveTypeCode.Char, PrimitiveTypeCode.SByte, PrimitiveTypeCode.Byte,
                PrimitiveTypeCode.Int16, PrimitiveTypeCode.UInt16, PrimitiveTypeCode.Object, PrimitiveTypeCode.String })
                if (named.Definition == Require(resolver.ResolveRuntimeType(library, code), work, offset))
                    return new TypeSignature.Primitive(code);
            return named.Kind == SignatureTypeKind.Unknown
                ? named with { Kind = Inline(named.Definition, work, offset) ? SignatureTypeKind.ValueType : SignatureTypeKind.Class } : named;
        }

        private IrType Storage(TypeSignature signature, Work? caller, int offset, bool borrow = false, bool allowVoid = false) => signature switch
        {
            TypeSignature.Named value when EnumScalar(value, caller, offset) => IrType.Int32,
            TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 } => IrType.Int32,
            TypeSignature.Primitive { Code: PrimitiveTypeCode.UInt32 } when integers64 => IrType.Int32,
            TypeSignature.Primitive { Code: PrimitiveTypeCode.Int64 or PrimitiveTypeCode.UInt64 } when integers64 => IrType.Int64,
            TypeSignature.Primitive primitive when integers64 && IntegerCasts.NarrowStorage(primitive) is not null => IrType.Int32,
            TypeSignature.Primitive { Code: PrimitiveTypeCode.String } when exceptions => IrType.Reference(IncludeString(caller, offset)),
            TypeSignature.Primitive { Code: PrimitiveTypeCode.Void } when allowVoid => IrType.Void,
            TypeSignature.Generic { Definition.Kind: SignatureTypeKind.Class } generic when generics => IrType.Reference(IncludeReference(new ConstructedType(generic.Definition.Definition, generic.Arguments), caller, offset)),
            TypeSignature.Generic { Definition.Kind: SignatureTypeKind.ValueType } generic when generics => IrType.Value(Include(new ConstructedType(generic.Definition.Definition, generic.Arguments), caller, offset)),
            TypeSignature.Named { Kind: SignatureTypeKind.ValueType } value => IrType.Value(Include(value.Definition, caller, offset)),
            TypeSignature.Named { Kind: SignatureTypeKind.Class } reference when classes => IrType.Reference(IncludeReference(reference.Definition, caller, offset)),
            TypeSignature.Primitive { Code: PrimitiveTypeCode.Object } when classes => IrType.Reference(IncludeReference(
                Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Object), caller, offset), caller, offset)),
            TypeSignature.Vector { Element: TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 } } when arrays => IrType.Reference(IntegerArray()),
            TypeSignature.Vector vector when integers64 => IrType.Reference(Vector(vector.Element, caller, offset)),
            TypeSignature.ByReference reference when integers64 && borrow && IntegerCasts.NarrowStorage(reference.Element) is not null =>
                throw new Failure(new(DiagnosticIds.ExecutionSignatureUnsupported, DiagnosticSeverity.Error,
                    "Narrow by-reference storage requires storage-width-aware carriers before execution." + Path(caller), Location(caller, offset))),
            TypeSignature.ByReference reference when borrow && IntegerCasts.NarrowStorage(reference.Element) is null => IrType.Location(Storage(reference.Element, caller, offset)),
            _ => throw new Failure(new(DiagnosticIds.ExecutionSignatureUnsupported, DiagnosticSeverity.Error,
                "Initial value CIL signatures require Int32 or named nongeneric structs, void returns, and synchronous byref parameters. "
                + (classes ? "The object profile additionally accepts nominal classes/Object and Int32 vectors; other reference signatures"
                    : arrays ? "The array profile additionally accepts Int32 vectors; other reference signatures" : "Reference signatures")
                + ", modifiers, byref locals/returns and generics require later lowering." + Path(caller), Location(caller, offset))),
        };

        private readonly Dictionary<ObjectTypeId, ObjectEnumArrayType> _enumArrays = [];

        private ObjectTypeId Vector(TypeSignature signature, Work? caller, int offset)
        {
            if (EnumScalar(signature, caller, offset))
            {
                var definition = ((TypeSignature.Named)signature).Definition;
                var id = ObjectModelBuilder.Id(definition);
                if (!_enumArrays.ContainsKey(id))
                {
                    var input = ReadEnumInput(definition, caller, offset);
                    _enumArrays.Add(id, new(input.Source, input.Attributes, input.LexicalOwners));
                }
                var enumeration = ObjectTypeId.Vector(ObjectBase(), ObjectArrayElement.Enum32(id));
                _vectors.Add(enumeration);
                if (_vectors.Count + _values.Count + _references.Count + _enumArrays.Count > ObjectModelLimits.MaximumTypes)
                    Fail(caller, "Combined enum/vector/value/reference type limit exceeded.", offset, DiagnosticIds.TranslationLimit);
                return enumeration;
            }
            if (signature is TypeSignature.Named named)
            {
                var definition = named.Definition;
                if (definition == Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Int32), caller, offset)) return IntegerArray();
                foreach (var narrowCode in new[] { PrimitiveTypeCode.Byte, PrimitiveTypeCode.Char, PrimitiveTypeCode.Int64 })
                    if (definition == Require(resolver.ResolveRuntimeType(library, narrowCode), caller, offset))
                        return Vector(new TypeSignature.Primitive(narrowCode), caller, offset);
                foreach (var code in new[] { PrimitiveTypeCode.UInt32, PrimitiveTypeCode.UInt64,
                    PrimitiveTypeCode.Boolean, PrimitiveTypeCode.Char, PrimitiveTypeCode.SByte, PrimitiveTypeCode.Byte, PrimitiveTypeCode.Int16, PrimitiveTypeCode.UInt16 })
                    if (definition == Require(resolver.ResolveRuntimeType(library, code), caller, offset))
                        Fail(caller, "Composed vector elements require Int32, an admitted nominal reference or a closed inline value; primitive-width-specific arrays require later lowering.", offset);
                if (definition == Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.String), caller, offset))
                    signature = new TypeSignature.Primitive(PrimitiveTypeCode.String);
                else if (named.Kind == SignatureTypeKind.Unknown)
                    signature = named with { Kind = Inline(definition, caller, offset) ? SignatureTypeKind.ValueType : SignatureTypeKind.Class };
            }
            if (signature is TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 }) return IntegerArray();
            if (signature is TypeSignature.Primitive { Code: PrimitiveTypeCode.Byte or PrimitiveTypeCode.Char or PrimitiveTypeCode.Int64 } narrow)
            {
                var numericVector = ObjectTypeId.Vector(ObjectBase(), narrow.Code == PrimitiveTypeCode.Int64 ? ObjectArrayElement.Int64 : narrow.Code == PrimitiveTypeCode.Byte ? ObjectArrayElement.Byte : ObjectArrayElement.Char);
                _vectors.Add(numericVector);
                if (_vectors.Count + _values.Count + _references.Count > ObjectModelLimits.MaximumTypes)
                    Fail(caller, "Combined vector/value/reference type limit exceeded.", offset, DiagnosticIds.TranslationLimit);
                return numericVector;
            }
            if (signature is TypeSignature.Vector or TypeSignature.ByReference)
                Fail(caller, "Nested vectors and byref array elements require later lowering.", offset);
            if (signature is TypeSignature.Generic generic && caller is not null)
            {
                try { _ = ClosedGenericKey.Create(generic.Arguments, SignatureList<TypeSignature>.Empty); }
                catch (ArgumentException error) { Fail(caller, error.Message, offset, DiagnosticIds.TranslationLimit); }
                CheckArgumentAccess(signature, caller, offset);
            }
            var element = Storage(signature, caller, offset);
            if (element.Kind is not (IrTypeKind.Reference or IrTypeKind.Value) || element.Class!.ArrayElement is not null)
                Fail(caller, "Composed vector elements require Int32, an admitted nominal reference or a closed inline value; other integer and enum arrays require later lowering.", offset);
            var vector = ObjectTypeId.Vector(ObjectBase(), element.Kind == IrTypeKind.Value
                ? ObjectArrayElement.Value(element.Class!) : ObjectArrayElement.Reference(element.Class!));
            _vectors.Add(vector);
            if (_vectors.Count + _values.Count + _references.Count > ObjectModelLimits.MaximumTypes)
                Fail(caller, "Combined vector/value/reference type limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            return vector;
        }

        private void CheckScalarOperandAccess(TypeSignature signature, Work work, int offset)
        {
            // A direct enum operand is a lexical type use, not a generic argument.
            // Reuse the same assembly/nesting proof as direct enum memory operands.
            if (integers64 && EnumScalar(signature, work, offset)) _ = OperandSignature(signature, work, offset);
            else CheckArgumentAccess(signature, work, offset);
        }

        private void CheckArgumentAccess(TypeSignature signature, Work work, int offset)
        {
            // The key validator bounds argument depth/nodes before this walk.
            var pending = new Stack<TypeSignature>(); pending.Push(signature);
            int count = 0;
            while (pending.TryPop(out var item))
            {
                if (++count > 4096) Fail(work, "Generic argument access exceeds the signature-node limit.", offset, DiagnosticIds.TranslationLimit);
                switch (item)
                {
                    case TypeSignature.Named named:
                        // Explicit reference names were checked before substitution. An opaque
                        // reference T is not a new lexical use in the generic library's image.
                        if (integers64 && (named.Kind == SignatureTypeKind.Class
                            || named.Kind == SignatureTypeKind.Unknown && !Inline(named.Definition, work, offset) && !EnumScalar(named, work, offset)))
                            break;
                        if (integers64 && EnumScalar(named, work, offset))
                        {
                            _ = OperandSignature(named, work, offset);
                            break;
                        }
                        if (integers64)
                        {
                            CheckValueDeclarationAccess(named.Definition, work, offset);
                            break;
                        }
                        var input = TypeAccess(named.Definition, work, offset);
                        if (!input.ExternallyVisible && (input.Source.ContentHash != work.Binding.Owner.ContentHash || input.Source.Token != work.Binding.Owner.Token))
                            Fail(work, "Generic arguments require the same declaring owner or externally visible types.", offset);
                        break;
                    case TypeSignature.Generic generic:
                        pending.Push(generic.Definition);
                        foreach (var argument in generic.Arguments) pending.Push(argument);
                        break;
                    case TypeSignature.Vector vector: pending.Push(vector.Element); break;
                }
            }
        }

        private ObjectTypeId Include(ResolvedType type, Work? work, int offset) => Include(new ConstructedType(type, SignatureList<TypeSignature>.Empty), work, offset);

        private ObjectTypeId ClosedId(ConstructedType type, Work? work, int offset)
        {
            try { return ObjectModelBuilder.Id(type); }
            catch (ArgumentException error) { Fail(work, error.Message, offset, DiagnosticIds.TranslationLimit); return null!; }
        }

        private ObjectTypeId Include(ConstructedType type, Work? work, int offset)
        {
            var id = ClosedId(type, work, offset);
            if (!_values.ContainsKey(id) && _values.Count + _references.Count >= ObjectModelLimits.MaximumTypes)
                Fail(work, "Value type root limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            _values.TryAdd(id, type); return id;
        }

        private ObjectTypeId IncludeReference(ResolvedType type, Work? work, int offset) => IncludeReference(new ConstructedType(type, SignatureList<TypeSignature>.Empty), work, offset);

        private ObjectTypeId IncludeReference(ConstructedType type, Work? work, int offset)
        {
            var id = ClosedId(type, work, offset);
            if (!_references.ContainsKey(id) && _values.Count + _references.Count >= ObjectModelLimits.MaximumTypes)
                Fail(work, "Combined value/reference root limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            if (exceptions && type.Arguments.Count == 0 && _stringType == id) { _references.TryAdd(id, type); return id; }
            if (!delegates || DelegateType(type, work, offset) is null) _ = SnapshotClosed(type, work, offset);
            if (exceptions) IncludeExceptionHierarchy(type, work, offset);
            _references.TryAdd(id, type); return id;
        }

        private bool Inline(ResolvedType type, Work? work, int offset)
        {
            // Classification needs only the selected direct base. A closed receiver
            // validates its constructed layout later, without retaining an open layout.
            var input = TypeBase(type, work, offset);
            _valueBase ??= ObjectModelBuilder.Id(Require(resolver.ResolveType(library, "System", "ValueType"), work, offset));
            return input.BaseType is TypeSignature.Named parent && ObjectModelBuilder.Id(parent.Definition) == _valueBase;
        }

        private void FailTypeSnapshot(string phase, ObjectTypeId attempted, Work? work, int offset)
        {
            int closed = _snapshots.Keys.Count(id => id.Specialization.Length != 0);
            int declarations = _snapshots.Keys.Select(id => (id.ContentHash, id.Token)).Distinct().Count();
            int enumScalars = _enumScalars.Count(pair => pair.Value);
            var outsideRoots = _snapshots.Where(pair => !_values.ContainsKey(pair.Key) && !_references.ContainsKey(pair.Key)).ToArray();
            var images = _snapshots.Keys.GroupBy(id => id.ContentHash).OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal).ToArray();
            string distribution = string.Join(", ", images.Take(8).Select(group => group.Key + "=" + group.Count().ToString(CultureInfo.InvariantCulture)))
                + (images.Length > 8 ? ", additional images=" + (images.Length - 8).ToString(CultureInfo.InvariantCulture) : "");
            string sample = string.Join(", ", outsideRoots.OrderBy(pair => pair.Value.Source.Name, StringComparer.Ordinal)
                .ThenBy(pair => pair.Key.ContentHash, StringComparer.Ordinal).ThenBy(pair => pair.Key.Token)
                .ThenBy(pair => pair.Key.Specialization, StringComparer.Ordinal).Take(8)
                .Select(pair => pair.Value.Source.Name.Length > 128 ? pair.Value.Source.Name[..128] + "..." : pair.Value.Source.Name));
            Fail(work, FormattableString.Invariant($"{phase} metadata limit exceeded. Cached {_snapshots.Count}/{ObjectModelLimits.MaximumTypes} type snapshots: {declarations} distinct nominal declarations, {closed} closed generic snapshots; {_snapshotFields}/{ObjectModelLimits.MaximumFields} field declarations; {_metadata.Remaining}/{ObjectModelLimits.MaximumMetadataMembers} metadata work units remaining. Explicit roots: {_values.Count} value, {_references.Count} reference, {_vectors.Count} vector. Validated Int32 enums: {enumScalars}. Snapshots outside explicit value/reference roots: {outsideRoots.Length} (sample: {sample}). Partial discovery only; snapshots and explicit roots are not emitted runtime layouts. Per-image snapshots: {distribution}. Attempted identity: {attempted.ContentHash}:0x{attempted.Token:x8}:{attempted.Specialization}."), offset, DiagnosticIds.TranslationLimit);
        }

        private TypeInput SnapshotClosed(ConstructedType type, Work? work, int offset)
        {
            if (type.Arguments.Count == 0) return Snapshot(type.Definition, work, offset);
            var id = ClosedId(type, work, offset);
            if (_snapshots.TryGetValue(id, out var known)) return known;
            if (_snapshots.Count >= ObjectModelLimits.MaximumTypes || _metadata.Remaining == 0)
                FailTypeSnapshot("Closed reference", id, work, offset);
            var input = Require(resolver.ReadClosedTypeInput(type, library,
                new(MaximumFields: ObjectModelLimits.MaximumFields - _snapshotFields, MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) FailTypeSnapshot("Closed reference", id, work, offset);
            _snapshotFields += input.Fields.Length;
            _snapshots.Add(id, input); return input;
        }

        private TypeInput Snapshot(ResolvedType type, Work? work, int offset)
        {
            var id = ObjectModelBuilder.Id(type);
            if (_snapshots.TryGetValue(id, out var known)) return known;
            if (_snapshots.Count >= ObjectModelLimits.MaximumTypes || _metadata.Remaining == 0)
                FailTypeSnapshot("Type classification", id, work, offset);
            var input = Require(resolver.ReadTypeInput(type.Assembly, type.Handle,
                new(MaximumFields: ObjectModelLimits.MaximumFields - _snapshotFields, MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) FailTypeSnapshot("Type classification", id, work, offset);
            _snapshotFields += input.Fields.Length;
            _snapshots.Add(id, input); return input;
        }
        private ResolvedField BoundField(Work work, int token, int offset)
        {
            var key = (work.Assembly.ContentHash, token, generics ? work.Binding.Method : null);
            if (!_fields.TryGetValue(key, out var field))
            {
                field = Require(generics ? resolver.ResolveClosedField(work.Resolved, MetadataTokens.EntityHandle(token), _metadata)
                    : resolver.ResolveDeclaredField(work.Assembly, MetadataTokens.EntityHandle(token), _metadata), work, offset);
                _fields.Add(key, field);
            }
            return field;
        }

        private static int Token(Work work, MethodInstruction instruction, params int[] kinds)
        {
            int token = instruction.IntegerOperand ?? 0;
            if ((token & 0xffffff) == 0 || !kinds.Contains((int)((uint)token >> 24)))
                Fail(work, "Instruction '" + instruction.OpCode + "' requires a supported non-nil metadata token; constructed type operands require later lowering.", instruction.Offset, DiagnosticIds.InvalidOperand);
            return token;
        }
        private static T Require<T>(ResolutionResult<T> result, Work? work = null, int offset = 0) where T : class =>
            result.Value ?? throw new Failure(result.Diagnostics[0] with
            { Message = result.Diagnostics[0].Message + Path(work), Location = work is null ? result.Diagnostics[0].Location
                : result.Diagnostics[0].Location is { } detail ? detail + "; requested from " + Location(work, offset) : Location(work, offset) });
        private static string Location(Work? work, int offset) => work is null ? "entry" :
            work.Input.Source.Name + " token 0x" + work.Input.Source.Token.ToString("x8", CultureInfo.InvariantCulture)
            + (work.Input.Source.Specialization.Length == 0 ? "" : " specialization " + work.Input.Source.Specialization)
            + " IL_" + offset.ToString("x4", CultureInfo.InvariantCulture);
        private static string Path(Work? work)
        {
            var path = new Stack<string>();
            for (var current = work; current?.Parent is not null; current = current.Parent) path.Push(Location(current.Parent, current.Offset));
            return path.Count == 0 ? "" : " Reachable via: " + string.Join(" -> ", path) + ".";
        }
        private static void Fail(Work? work, string message, int offset = 0, string code = DiagnosticIds.ExecutionUnsupported) =>
            throw new Failure(new(code, DiagnosticSeverity.Error, message + Path(work), Location(work, offset)));
    }
}
