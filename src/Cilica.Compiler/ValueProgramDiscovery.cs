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
    public ImmutableDictionary<int, IrType> ReferenceBoxOperands { get; init; } = ImmutableDictionary<int, IrType>.Empty;
    public ImmutableDictionary<int, ClosedKeyIntrinsic> KeyOperations { get; init; } = ImmutableDictionary<int, ClosedKeyIntrinsic>.Empty;
    public ImmutableDictionary<int, ConstrainedValueCall> ConstrainedCalls { get; init; } = ImmutableDictionary<int, ConstrainedValueCall>.Empty;
}
public sealed record ConstrainedValueCall(int PrefixOffset, ObjectTypeId Receiver);

/// <summary>A bounded metadata discovery result, not an executable program or access-proof supplied by a caller.</summary>
public enum ValueIntrinsic { NewException, ExceptionMessage, StringLength, StringEqual, StringCodeUnit, StringConcat, StringNotEqual, ExceptionParameterName, InvariantCulture, IntegerTextFind, IntegerTextPublish, StringFromCodeUnits, EmptyArray, NewTypeInitializationException, ExceptionInnerException, ExceptionTypeName }
public sealed record ValueIntrinsicCall(ValueIntrinsic Kind, ObjectTypeId Type, int Parameters,
    int MessageArgument = -1, int ParameterArgument = -1);
public sealed record ValueProgramInput(IrMethodId Entry, ImmutableArray<ValueMethodInput> Methods, ValueTypeCatalog Types)
{
    public ManagedExceptionTypes? Exceptions { get; init; }
    public ObjectInterfaceDispatchPlan? InterfaceDispatch { get; init; }
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
        private readonly Dictionary<(string Hash, int Token), ResolvedMethod> _calls = [];
        private readonly Dictionary<(string Hash, int Token), ResolvedField> _fields = [];
        private readonly Dictionary<(string Hash, int Token), ResolvedType> _types = [];
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
                    var calls = ImmutableDictionary.CreateBuilder<int, IrMethodId>();
                    var fields = ImmutableDictionary.CreateBuilder<int, ObjectFieldId>();
                    var types = ImmutableDictionary.CreateBuilder<int, ObjectTypeId>();
                    var callable = ImmutableDictionary.CreateBuilder<int, ObjectTypeId>();
                    var vectors = ImmutableDictionary.CreateBuilder<int, ObjectTypeId>();
                    var intrinsics = ImmutableDictionary.CreateBuilder<int, ValueIntrinsicCall>();
                    var literals = ImmutableDictionary.CreateBuilder<int, ImmutableArray<ushort>>();
                    var interfaceCalls = ImmutableDictionary.CreateBuilder<int, ObjectInterfaceDeclaration>();
                    var memory = ImmutableDictionary.CreateBuilder<int, IrType>();
                    var defaultStatics = ImmutableDictionary.CreateBuilder<int, IrType>();
                    var initializers = ImmutableDictionary.CreateBuilder<int, IrMethodId>();
                    var staticFields = ImmutableDictionary.CreateBuilder<int, ObjectStaticField>();
                    var referenceBoxes = ImmutableDictionary.CreateBuilder<int, IrType>();
                    var constrained = ConstrainedPrefixes(work);
                    var constrainedCalls = ImmutableDictionary.CreateBuilder<int, ConstrainedValueCall>();
                    var keyOperations = ImmutableDictionary.CreateBuilder<int, ClosedKeyIntrinsic>();
                    foreach (var instruction in work.Input.Instructions)
                    {
                        var offset = instruction.Offset;
                        if (exceptions && instruction.OpCode == "ldstr")
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
                            types.Add(offset, ReferenceOperand(signature, work, offset));
                        }
                        else if (arrays && instruction.OpCode == "newarr")
                        {
                            if (integers64)
                            {
                                var signature = Require(resolver.ResolveClosedTypeOperand(work.Resolved, MetadataTokens.EntityHandle(Token(work, instruction, 0x01, 0x02, 0x1b))), work, offset);
                                vectors.Add(offset, Vector(signature, work, offset)); continue;
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
                        else if (integers64 && instruction.OpCode is ("ldelem.ref" or "stelem.ref")) _ = Vector(new TypeSignature.Primitive(PrimitiveTypeCode.Object), work, offset);
                        else if (integers64 && instruction.OpCode is ("ldelem" or "stelem"))
                        {
                            var signature = Require(resolver.ResolveClosedTypeOperand(work.Resolved, MetadataTokens.EntityHandle(Token(work, instruction, 0x01, 0x02, 0x1b))), work, offset);
                            vectors.Add(offset, Vector(signature, work, offset));
                        }
                        else if (instruction.OpCode == "call" || classes && instruction.OpCode == "newobj" || delegates && instruction.OpCode is ("ldftn" or "callvirt"))
                        {
                            int token = Token(work, instruction, 0x06, 0x0a, 0x2b);
                            var key = (work.Assembly.ContentHash, token);
                            if (generics || !_calls.TryGetValue(key, out _))
                            {
                                _calls[key] = Require(generics
                                    ? resolver.ResolveClosedCall(work.Resolved, MetadataTokens.EntityHandle(token), _metadata)
                                    : resolver.ResolveDeclaredMethod(work.Assembly, MetadataTokens.EntityHandle(token), _metadata), work, offset);
                            }
                            var target = _calls[key];
                            if (KeyOperation(work, instruction, ref target) is { } keyOperation)
                            {
                                _arguments += keyOperation.Operation == ClosedKeyOperation.Equal ? 2 : 1;
                                if (_arguments > ProgramLimits.MaximumCallArguments) Fail(work, "Aggregate key intrinsic argument limit exceeded.", offset, DiagnosticIds.TranslationLimit);
                                keyOperations.Add(offset, keyOperation); continue;
                            }
                            if (constrained.TryGetValue(offset, out var prefix))
                            {
                                var signature = Require(resolver.ResolveClosedTypeOperand(work.Resolved,
                                    MetadataTokens.EntityHandle(Token(work, prefix, 0x01, 0x02, 0x1b))), work, prefix.Offset);
                                var receiver = signature switch {
                                    TypeSignature.Named { Kind: SignatureTypeKind.ValueType or SignatureTypeKind.Unknown } named => new ConstructedType(named.Definition, SignatureList<TypeSignature>.Empty),
                                    TypeSignature.Generic { Definition.Kind: SignatureTypeKind.ValueType } generic => new ConstructedType(generic.Definition.Definition, generic.Arguments),
                                    _ => null };
                                if (receiver is null) Fail(work, "Constrained direct calls require a closed inline value receiver; primitive/reference fallbacks require later lowering.", prefix.Offset);
                                var exact = Require(resolver.ResolveConstrainedValueTarget(target, receiver!, library, _metadata), work, offset);
                                target = exact.Implementation;
                                constrainedCalls.Add(offset, new(prefix.Offset, Include(receiver!, work, prefix.Offset)));
                            }
                            if (generics)
                            {
                                try { _ = ClosedGenericKey.Create(target.DeclaringType.Arguments, target.MethodArguments); }
                                catch (ArgumentException error) { Fail(work, error.Message, offset, DiagnosticIds.TranslationLimit); }
                                foreach (var argument in target.DeclaringType.Arguments.Concat(target.MethodArguments))
                                    CheckArgumentAccess(argument, work, offset);
                            }
                            if (exceptions && (EmptyArrayCall(work, instruction, target) ?? StringConstructionCall(work, instruction, target) ?? IntegerCacheCall(work, instruction, target) ?? CultureCall(work, instruction, target) ?? ExceptionCall(work, instruction, target)) is { } intrinsic)
                            {
                                _arguments += intrinsic.Parameters;
                                if (_arguments > ProgramLimits.MaximumCallArguments) Fail(work, "Aggregate intrinsic argument limit exceeded.", offset, DiagnosticIds.TranslationLimit);
                                intrinsics.Add(offset, intrinsic); continue;
                            }
                            if (delegates && DelegateType(target.DeclaringType, work, offset) is { } declaration)
                            {
                                var facts = _delegateFacts[ObjectTypeId.From(declaration.Source)];
                                if (!(instruction.OpCode == "newobj" && target.Handle == facts.Constructor
                                    || instruction.OpCode == "callvirt" && target.Handle == facts.Invoke))
                                    Fail(work, "Delegate members require paired ldftn/newobj construction or callvirt Invoke; multicast and other members are unsupported.", offset);
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
                            target = NarrowVirtualReceiver(work, instruction, target);
                            var callee = Discover(target, work, offset);
                            if (instruction.OpCode == "callvirt" && callee.Binding.Kind != (constrainedCalls.ContainsKey(offset) ? ObjectMethodKind.ValueInstance : ObjectMethodKind.Instance))
                                Fail(work, "callvirt requires a class instance method.", offset);
                            if (instruction.OpCode == "callvirt" && callee.Binding.VirtualSlot is not null)
                                _virtualCalls.TryAdd(callee.Binding.Method, (work, offset));
                            if (instruction.OpCode == "newobj" && callee.Binding.Kind is not (ObjectMethodKind.Constructor or ObjectMethodKind.ObjectBaseConstructor or ObjectMethodKind.ValueConstructor))
                                Fail(work, "newobj requires an exact supported constructor declaration.", offset);
                            if (!ObjectMethodAccess.Allowed(work.Binding, callee.Binding, integers64))
                                Fail(work, "Calls require the same declaring owner, a public externally visible target, or an assembly-accessible target in the same image under the integer-object profile.", offset);
                            _arguments += instruction.OpCode == "ldftn" ? 0 : callee.Parameters.Length - (instruction.OpCode == "newobj" ? 1 : 0);
                            if (_arguments > ProgramLimits.MaximumCallArguments) Fail(work, "Aggregate call-argument limit exceeded.", offset, DiagnosticIds.TranslationLimit);
                            calls.Add(offset, callee.Binding.Method);
                        }
                        else if (integers64 && instruction.OpCode is ("ldsfld" or "stsfld"))
                        {
                            int token = Token(work, instruction, 0x04, 0x0a);
                            var field = Require(resolver.ResolveClosedField(work.Resolved, MetadataTokens.EntityHandle(token), _metadata), work, offset);
                            var owner = SnapshotClosed(field.DeclaringType, work, offset);
                            var declaration = owner.Fields.Single(f => f.Token == MetadataTokens.GetToken(field.Handle));
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
                            var key = (work.Assembly.ContentHash, token);
                            if (generics || !_fields.TryGetValue(key, out _))
                            {
                                _fields[key] = Require(generics ? resolver.ResolveClosedField(work.Resolved, MetadataTokens.EntityHandle(token), _metadata)
                                    : resolver.ResolveDeclaredField(work.Assembly, MetadataTokens.EntityHandle(token), _metadata), work, offset);
                            }
                            var field = _fields[key];
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
                                if (integers64 && instruction.OpCode is "initobj" or "ldobj" or "stobj" or "cpobj")
                                {
                                    signature = OperandSignature(signature, work, offset);
                                    CheckArgumentAccess(signature, work, offset);
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
                        calls.ToImmutable(), fields.ToImmutable(), types.ToImmutable()) { Initializers = initializers.ToImmutable(), StaticFields = staticFields.ToImmutable(), KeyOperations = keyOperations.ToImmutable(), DefaultStatics = defaultStatics.ToImmutable(), ConstrainedCalls = constrainedCalls.ToImmutable(), ReferenceBoxOperands = referenceBoxes.ToImmutable(), MemoryOperands = memory.ToImmutable(), InterfaceOperands = interfaceCalls.ToImmutable(), ArrayOperands = vectors.ToImmutable(), DelegateOperands = callable.ToImmutable(), ExceptionRegions = work.Regions, Intrinsics = intrinsics.ToImmutable(), Literals = literals.ToImmutable() });
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
                    }) : ValueModelBuilder.BuildCatalog(resolver, [.. _values.Values.Select(t => t.Definition)], library, _metadata, _integerArray is not null,
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
                    if (_methods.Keys.Concat(interfacePlan.Declarations.Select(d => IrMethodId.From(d.Source)))
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
                    var declaration = _methods[site.Key];
                    if (!_dispatch.TryGetValue(site.Key, out var targets)) _dispatch.Add(site.Key, targets = []);
                    foreach (var layout in catalog.References.Types)
                    {
                        var receiver = ObjectTypeId.From(layout.Source);
                        if (!relations.Assignable(IrType.Reference(receiver), IrType.Reference(declaration.Binding.Owner)) || targets.ContainsKey(receiver)) continue;
                        if (_dispatchTargetCount + interfaceTargets >= ObjectDispatchLimits.MaximumTargets)
                            Fail(site.Value.Caller, "Aggregate class/interface dispatch target limit exceeded.", site.Value.Offset, DiagnosticIds.TranslationLimit);
                        var exact = ResolveVirtual(declaration.Resolved, _knownTypes[receiver], site.Value.Caller, site.Value.Offset);
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
                    bool visible = layouts.TryGetValue(field.Id.Owner, out var inlineOwner) ? inlineOwner.ExternallyVisible : _snapshots[field.Id.Owner].ExternallyVisible
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
                        if (_snapshots.TryGetValue(element.Class, out var reference) && !reference.ExternallyVisible && element.Class != method.Binding.Owner
                            && (!integers64 || !ObjectLexicalAccess.Visible(method.Binding, element.Class, reference.LexicalOwners)))
                            Fail(work, "Class type use requires the same declaring owner or a fully externally visible type.");
                        if (generics && _references.TryGetValue(element.Class, out var constructed))
                            foreach (var argument in constructed.Arguments) CheckArgumentAccess(argument, work, 0);
                    }
                }
                foreach (var operand in method.TypeOperands)
                    if (layouts.TryGetValue(operand.Value, out var operandLayout))
                    {
                        CheckType(operand.Value, operand.Key);
                        if (!operandLayout.Interfaces.IsEmpty && work.Input.Instructions.Any(i => i.Offset == operand.Key && i.OpCode is "box" or "unbox" or "unbox.any"))
                            Fail(work, "Boxing interface-bearing values requires boxed interface relationships and dispatch before execution.", operand.Key);
                    }

                void CheckType(ObjectTypeId type, int offset)
                {
                    if (generics && _values.TryGetValue(type, out var constructed))
                        foreach (var argument in constructed.Arguments) CheckArgumentAccess(argument, work, offset);
                    if (type != method.Binding.Owner && !layouts[type].ExternallyVisible)
                        Fail(work, "Value type use requires the same declaring owner or a fully externally visible type; other visibility rules require later lowering.", offset);
                }
            }
            return new(root.Binding.Method, [.. methods.OrderBy(m => m.Binding.Method.ContentHash, StringComparer.Ordinal).ThenBy(m => m.Binding.Method.Token).ThenBy(m => m.Binding.Method.Specialization, StringComparer.Ordinal)], catalog)
                { InterfaceDispatch = interfacePlan,
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
                if (fact is not null && (known.Binding != fact.Binding || known.ReturnType != fact.ReturnType || !known.Parameters.SequenceEqual(fact.Parameters)))
                    Fail(parent, "Executable method contradicts its interface mapping facts.", offset);
                return known;
            }
            if (_methods.Count + _exceptionDeclarations.Count >= ProgramLimits.MaximumMethods || _bytes >= ProgramLimits.MaximumBodyBytes
                || _instructions >= ProgramLimits.MaximumInstructions || _metadata.Remaining == 0)
                Fail(parent, "Aggregate method, IL or metadata-member limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            var readLimits = limits with
            {
                MaximumBodyBytes = Math.Min(limits.MaximumBodyBytes, ProgramLimits.MaximumBodyBytes - _bytes),
                MaximumInstructions = Math.Min(limits.MaximumInstructions, ProgramLimits.MaximumInstructions - _instructions),
                MaximumInitializerMembers = Math.Min(limits.MaximumInitializerMembers, _metadata.Remaining),
            };
            ManagedExceptionMethodInput? protectedInput = exceptions ? Require(resolver.ReadManagedExceptionMethodInput(resolved, library, readLimits), parent, offset) : null;
            var input = protectedInput?.Method ?? Require(generics ? resolver.ReadClosedMethodInput(resolved, library, readLimits)
                : resolver.ReadMethodInput(owner.Assembly, resolved.Handle, readLimits), parent, offset);
            _bytes += input.CodeSize; _instructions += input.Instructions.Length;
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(parent, "Aggregate metadata-member limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            bool initializer = integers64 && input.DeclaredName == ".cctor";
            bool constructor = classes && input.DeclaredName == ".ctor";
            bool instance = constructor || delegates && (input.Attributes & MethodAttributes.Static) == 0;
            var allowed = MethodAttributes.MemberAccessMask | MethodAttributes.Static | MethodAttributes.HideBySig
                | (integers64 ? MethodAttributes.SpecialName : 0)
                | (integers64 || fact is not null ? MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Final : 0)
                | (constructor || initializer ? MethodAttributes.SpecialName | MethodAttributes.RTSpecialName : 0);
            if (input.IsReferenceAssembly || !generics && (input.OwnerGenericArity != 0 || input.Signature.GenericArity != 0) || input.Signature.Header != (instance ? 0x20 : generics && input.Signature.GenericArity != 0 ? 0x10 : 0)
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
                if (input.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String }
                    || input.Signature.Parameters.Count != 1 || input.Signature.Parameters[0] is not TypeSignature.Named provider
                    || provider.Definition != Require(resolver.ResolveType(library, "System", "IFormatProvider"), parent, offset))
                    Fail(parent, "Integer formatting requires the supported ToString(IFormatProvider) declaration.", offset);
                _ = Require(resolver.AuthorizeLibraryIntrinsic(library, resolved.Handle, LibraryIntrinsicCapability.InvariantIntegerFormatting), parent, offset);
                binding = binding with { InvariantProviderOnly = true };
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
                [.. input.Locals.Select(t => Storage(t, parent, offset))], parent, offset);
            if (fact is not null && (binding.Kind != kind || binding.Owner != ownerId || binding.Attributes != input.Attributes
                || work.ReturnType != fact.ReturnType || !work.Parameters.SequenceEqual(fact.Parameters)))
                Fail(parent, "Closed method signature contradicts its interface mapping facts.", offset);
            if (instance && !inlineConstructor && !integers64 && work.Parameters.Concat(work.Locals).Append(work.ReturnType).Any(t => t.Kind is IrTypeKind.Value or IrTypeKind.ManagedLocation))
                Fail(parent, "Class instance methods require scalar/reference parameters, locals and returns; values and borrows need later lowering.", offset);
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
                    "Narrow by-reference parameters require storage-width-aware carriers before execution." + Path(caller), Location(caller, offset))),
            TypeSignature.ByReference reference when borrow && IntegerCasts.NarrowStorage(reference.Element) is null => IrType.Location(Storage(reference.Element, caller, offset)),
            _ => throw new Failure(new(DiagnosticIds.ExecutionSignatureUnsupported, DiagnosticSeverity.Error,
                "Initial value CIL signatures require Int32 or named nongeneric structs, void returns, and synchronous byref parameters. "
                + (classes ? "The object profile additionally accepts nominal classes/Object and Int32 vectors; other reference signatures"
                    : arrays ? "The array profile additionally accepts Int32 vectors; other reference signatures" : "Reference signatures")
                + ", modifiers, byref locals/returns and generics require later lowering." + Path(caller), Location(caller, offset))),
        };

        private ObjectTypeId Vector(TypeSignature signature, Work? caller, int offset)
        {
            if (signature is TypeSignature.Named named)
            {
                var definition = named.Definition;
                if (definition == Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Int32), caller, offset)) return IntegerArray();
                if (definition == Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.String), caller, offset))
                    signature = new TypeSignature.Primitive(PrimitiveTypeCode.String);
                else if (named.Kind == SignatureTypeKind.Unknown && !Inline(definition, caller, offset))
                    signature = named with { Kind = SignatureTypeKind.Class };
            }
            if (signature is TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 }) return IntegerArray();
            if (signature is TypeSignature.Vector or TypeSignature.ByReference)
                Fail(caller, "Nested vectors and byref array elements require later lowering.", offset);
            if (signature is TypeSignature.Generic generic && caller is not null)
            {
                try { _ = ClosedGenericKey.Create(generic.Arguments, SignatureList<TypeSignature>.Empty); }
                catch (ArgumentException error) { Fail(caller, error.Message, offset, DiagnosticIds.TranslationLimit); }
                CheckArgumentAccess(signature, caller, offset);
            }
            var element = Storage(signature, caller, offset);
            if (element.Kind != IrTypeKind.Reference || element.Class!.ArrayElement is not null)
                Fail(caller, "Composed vector elements require Int32 or an admitted nominal reference; value and other integer arrays require later lowering.", offset);
            var vector = ObjectTypeId.Vector(ObjectBase(), ObjectArrayElement.Reference(element.Class!));
            _vectors.Add(vector);
            if (_vectors.Count + _values.Count + _references.Count > ObjectModelLimits.MaximumTypes)
                Fail(caller, "Combined vector/value/reference type limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            return vector;
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
                        var input = Snapshot(named.Definition, work, offset);
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
            var input = Snapshot(type, work, offset);
            _valueBase ??= ObjectModelBuilder.Id(Require(resolver.ResolveType(library, "System", "ValueType"), work, offset));
            return input.BaseType is TypeSignature.Named parent && ObjectModelBuilder.Id(parent.Definition) == _valueBase;
        }

        private TypeInput SnapshotClosed(ConstructedType type, Work? work, int offset)
        {
            if (type.Arguments.Count == 0) return Snapshot(type.Definition, work, offset);
            var id = ClosedId(type, work, offset);
            if (_snapshots.TryGetValue(id, out var known)) return known;
            if (_snapshots.Count >= ObjectModelLimits.MaximumTypes || _metadata.Remaining == 0)
                Fail(work, "Closed reference metadata limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadClosedTypeInput(type, library,
                new(MaximumFields: ObjectModelLimits.MaximumFields - _snapshotFields, MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Closed reference metadata limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            _snapshotFields += input.Fields.Length;
            _snapshots.Add(id, input); return input;
        }

        private TypeInput Snapshot(ResolvedType type, Work? work, int offset)
        {
            var id = ObjectModelBuilder.Id(type);
            if (_snapshots.TryGetValue(id, out var known)) return known;
            if (_snapshots.Count >= ObjectModelLimits.MaximumTypes || _metadata.Remaining == 0)
                Fail(work, "Type classification metadata limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadTypeInput(type.Assembly, type.Handle,
                new(MaximumFields: ObjectModelLimits.MaximumFields - _snapshotFields, MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Type classification metadata limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            _snapshotFields += input.Fields.Length;
            _snapshots.Add(id, input); return input;
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
            { Message = result.Diagnostics[0].Message + Path(work), Location = work is null ? result.Diagnostics[0].Location : Location(work, offset) });
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
