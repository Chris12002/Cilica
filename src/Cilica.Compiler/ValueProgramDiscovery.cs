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
    public ImmutableDictionary<int, ObjectTypeId> ArrayOperands { get; init; } = ImmutableDictionary<int, ObjectTypeId>.Empty;
}

/// <summary>A bounded metadata discovery result, not an executable program or access-proof supplied by a caller.</summary>
public sealed record ValueProgramInput(IrMethodId Entry, ImmutableArray<ValueMethodInput> Methods, ValueTypeCatalog Types);
public sealed record ValueDiscoveryResult(ValueProgramInput? Input, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Input is not null;
}

/// <summary>Discovers bounded CIL value closures with opt-in class constructors without executing assemblies.</summary>
public static class ValueProgramDiscovery
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

    private static ValueDiscoveryResult ReadCore(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits, bool arrays, bool classes = false)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(runtimeLibrary);
        try { return new(new Session(resolver, runtimeLibrary, limits ?? new(), arrays, classes).Read(assembly, entry), []); }
        catch (Failure failure) { return new(null, [failure.Diagnostic]); }
    }

    private sealed class Failure(Diagnostic diagnostic) : Exception(diagnostic.Message)
    {
        internal Diagnostic Diagnostic { get; } = diagnostic;
    }

    private sealed class Session(AssemblyResolver resolver, ResolvedAssembly library, MethodReadLimits limits, bool arrays, bool classes)
    {
        private sealed record Work(ResolvedAssembly Assembly, MethodInput Input, ObjectMethodBinding Binding,
            ImmutableArray<IrType> Parameters, IrType ReturnType, ImmutableArray<IrType> Locals, Work? Parent, int Offset);
        private readonly MemberResolutionBudget _metadata = new(ObjectModelLimits.MaximumMetadataMembers);
        private readonly Dictionary<IrMethodId, Work> _methods = [];
        private readonly Queue<Work> _pending = new();
        private readonly Dictionary<ObjectTypeId, ResolvedType> _values = [];
        private readonly Dictionary<ObjectTypeId, ResolvedType> _references = [];
        private readonly Dictionary<ObjectTypeId, TypeInput> _snapshots = [];
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
            var resolved = Require(resolver.ResolveDeclaredMethod(assembly, entry, _metadata));
            var root = Discover(resolved, null, 0);
            if (root.Binding.Kind != ObjectMethodKind.Static || root.ReturnType != IrType.Int32 || root.Parameters.Any(t => t != IrType.Int32))
                Fail(root, "Value entry requires Int32 parameters and return; values and borrows remain internal.");
            var methods = new List<ValueMethodInput>();
            while (_pending.TryDequeue(out var work))
            {
                var calls = ImmutableDictionary.CreateBuilder<int, IrMethodId>();
                var fields = ImmutableDictionary.CreateBuilder<int, ObjectFieldId>();
                var types = ImmutableDictionary.CreateBuilder<int, ObjectTypeId>();
                var vectors = ImmutableDictionary.CreateBuilder<int, ObjectTypeId>();
                foreach (var instruction in work.Input.Instructions)
                {
                    var offset = instruction.Offset;
                    if (arrays && instruction.OpCode == "newarr")
                    {
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
                    else if (arrays && instruction.OpCode is ("ldelem.i4" or "stelem.i4" or "ldlen")) _ = IntegerArray();
                    else if (instruction.OpCode == "call" || classes && instruction.OpCode == "newobj")
                    {
                        int token = Token(work, instruction, 0x06, 0x0a, 0x2b);
                        var key = (work.Assembly.ContentHash, token);
                        if (!_calls.TryGetValue(key, out var target))
                        {
                            target = Require(resolver.ResolveDeclaredMethod(work.Assembly, MetadataTokens.EntityHandle(token), _metadata), work, offset);
                            _calls.Add(key, target);
                        }
                        var callee = Discover(target, work, offset);
                        if (instruction.OpCode == "newobj" && callee.Binding.Kind is not (ObjectMethodKind.Constructor or ObjectMethodKind.ObjectBaseConstructor))
                            Fail(work, "newobj requires an exact class constructor declaration.", offset);
                        if (work.Binding.Owner != callee.Binding.Owner
                            && ((callee.Binding.Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public || !callee.Binding.OwnerExternallyVisible))
                            Fail(work, "Calls require the same declaring owner or a public method on an externally visible owner.", offset);
                        _arguments += callee.Parameters.Length - (instruction.OpCode == "newobj" ? 1 : 0);
                        if (_arguments > ProgramLimits.MaximumCallArguments) Fail(work, "Aggregate call-argument limit exceeded.", offset, DiagnosticIds.TranslationLimit);
                        calls.Add(offset, callee.Binding.Method);
                    }
                    else if (instruction.OpCode is "ldfld" or "ldflda" or "stfld")
                    {
                        int token = Token(work, instruction, 0x04, 0x0a);
                        var key = (work.Assembly.ContentHash, token);
                        if (!_fields.TryGetValue(key, out var field))
                        {
                            field = Require(resolver.ResolveDeclaredField(work.Assembly, MetadataTokens.EntityHandle(token), _metadata), work, offset);
                            _fields.Add(key, field);
                        }
                        if (field.DeclaringType.Arguments.Count != 0) Fail(work, "Generic field owners require later lowering.", offset);
                        var owner = field.DeclaringType.Definition;
                        fields.Add(offset, new(classes && !Inline(owner, work, offset) ? IncludeReference(owner, work, offset)
                            : Include(owner, work, offset), MetadataTokens.GetToken(field.Handle)));
                    }
                    else if (instruction.OpCode is "initobj" or "ldobj" or "stobj" or "cpobj")
                    {
                        int token = Token(work, instruction, 0x01, 0x02);
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
                    calls.ToImmutable(), fields.ToImmutable(), types.ToImmutable()) { ArrayOperands = vectors.ToImmutable() });
            }
            if (_values.Count == 0) Fail(root, "Value discovery requires an inline value; use the existing primitive/object translator for other programs.");
            var built = ValueModelBuilder.BuildCatalog(resolver, [.. _values.Values], library, _metadata, _integerArray is not null,
                [.. _references.Values], _snapshots);
            if (built.Catalog is not { } catalog) throw new Failure(built.Diagnostics[0]);
            var layouts = catalog.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
            var declarations = catalog.Types.SelectMany(t => t.Fields).Concat(catalog.References.Types.SelectMany(t => t.Fields)).ToDictionary(f => f.Id);
            foreach (var method in methods)
            {
                var work = _methods[method.Binding.Method];
                var writes = work.Input.Instructions.Where(i => i.OpCode == "stfld").Select(i => i.Offset).ToHashSet();
                foreach (var access in method.Fields)
                {
                    var field = declarations[access.Value];
                    bool visible = layouts.TryGetValue(field.Id.Owner, out var inlineOwner) ? inlineOwner.ExternallyVisible : _snapshots[field.Id.Owner].ExternallyVisible;
                    if ((field.Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.PrivateScope
                        || method.Binding.Owner != field.Id.Owner
                        && ((field.Attributes & FieldAttributes.FieldAccessMask) != FieldAttributes.Public || !visible))
                        Fail(work, "Fields require the same declaring owner or a public member on an externally visible type.", access.Key);
                    // Taking an address retains readonly capability in body translation.
                    if ((field.Attributes & FieldAttributes.InitOnly) != 0
                        && writes.Contains(access.Key) && !(method.Binding.Kind == ObjectMethodKind.Constructor && method.Binding.Owner == field.Id.Owner))
                        Fail(work, classes ? "Readonly fields require the declaring class constructor and its checked receiver."
                            : "Readonly value fields cannot be written by this static-method profile.", access.Key);
                    if (field.Storage is ObjectStorage.Value inline) CheckType(inline.Type, access.Key);
                }
                foreach (var type in method.Parameters.Concat(method.Locals).Append(method.ReturnType))
                {
                    var element = type.Kind == IrTypeKind.ManagedLocation ? type.Element! : type;
                    if (element.Kind == IrTypeKind.Value) CheckType(element.Class!, 0);
                    else if (classes && element.Kind == IrTypeKind.Reference && element.Class!.ArrayElement is null
                        && _snapshots.TryGetValue(element.Class, out var reference) && !reference.ExternallyVisible && element.Class != method.Binding.Owner)
                        Fail(work, "Class type use requires the same declaring owner or a fully externally visible type.");
                }
                foreach (var operand in method.TypeOperands) CheckType(operand.Value, operand.Key);

                void CheckType(ObjectTypeId type, int offset)
                {
                    if (type != method.Binding.Owner && !layouts[type].ExternallyVisible)
                        Fail(work, "Value type use requires the same declaring owner or a fully externally visible type; other visibility rules require later lowering.", offset);
                }
            }
            return new(root.Binding.Method, [.. methods.OrderBy(m => m.Binding.Method.ContentHash, StringComparer.Ordinal).ThenBy(m => m.Binding.Method.Token)], catalog);
        }

        private Work Discover(ResolvedMethod resolved, Work? parent, int offset)
        {
            var owner = resolved.DeclaringType.Definition;
            var id = new IrMethodId(owner.Assembly.ContentHash, MetadataTokens.GetToken(resolved.Handle));
            // Check instantiation before the definition-key cache: a generic call must never alias an ordinary one.
            if (resolved.DeclaringType.Arguments.Count != 0 || resolved.MethodArguments.Count != 0)
                Fail(parent, "Generic method/type instantiations require later lowering.", offset);
            if (_methods.TryGetValue(id, out var known)) return known;
            if (_methods.Count >= ProgramLimits.MaximumMethods || _bytes >= ProgramLimits.MaximumBodyBytes
                || _instructions >= ProgramLimits.MaximumInstructions || _metadata.Remaining == 0)
                Fail(parent, "Aggregate method, IL or metadata-member limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodInput(owner.Assembly, resolved.Handle, limits with
            {
                MaximumBodyBytes = Math.Min(limits.MaximumBodyBytes, ProgramLimits.MaximumBodyBytes - _bytes),
                MaximumInstructions = Math.Min(limits.MaximumInstructions, ProgramLimits.MaximumInstructions - _instructions),
                MaximumInitializerMembers = Math.Min(limits.MaximumInitializerMembers, _metadata.Remaining),
            }), parent, offset);
            _bytes += input.CodeSize; _instructions += input.Instructions.Length;
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(parent, "Aggregate metadata-member limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            bool constructor = classes && input.DeclaredName == ".ctor";
            var allowed = MethodAttributes.MemberAccessMask | MethodAttributes.Static | MethodAttributes.HideBySig
                | (constructor ? MethodAttributes.SpecialName | MethodAttributes.RTSpecialName : 0);
            if (input.IsReferenceAssembly || input.OwnerGenericArity != 0 || input.Signature.GenericArity != 0 || input.Signature.Header != (constructor ? 0x20 : 0)
                || input.HasTypeInitializer || input.HasModuleInitializer || input.ExceptionRegionCount != 0
                || !input.Locals.IsEmpty && !input.InitializeLocals
                || ((input.Attributes & MethodAttributes.Static) != 0) == constructor
                || (input.Attributes & ~allowed) != 0
                || (input.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.PrivateScope
                || (input.Implementation & (MethodImplAttributes.CodeTypeMask | MethodImplAttributes.ManagedMask | MethodImplAttributes.InternalCall | MethodImplAttributes.Synchronized)) != MethodImplAttributes.IL)
                Fail(parent, classes
                    ? "Value/object methods require ordinary static methods or exact class constructors, nongeneric managed IL, no type/module initializers, no exception regions and initialized locals."
                    : "Value methods require static nongeneric ordinary managed IL without initialization, special names, exception regions or uninitialized locals.", offset);
            var ownerId = ObjectModelBuilder.Id(owner);
            if (constructor)
            {
                if ((input.Attributes & (MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)) != (MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)
                    || Inline(owner, parent, offset) || input.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void })
                    Fail(parent, "Class constructors require exact constructor flags, a class receiver and void return.", offset);
                IncludeReference(owner, parent, offset);
            }
            _parameters += input.Signature.Parameters.Count + (constructor ? 1 : 0); _locals += input.Locals.Length;
            if (_parameters > IrLimits.MaximumParameters || _locals > IrLimits.MaximumLocals)
                Fail(parent, "Aggregate parameter/local storage limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            var kind = !constructor ? ObjectMethodKind.Static : ownerId == ObjectBase() ? ObjectMethodKind.ObjectBaseConstructor : ObjectMethodKind.Constructor;
            var binding = new ObjectMethodBinding(id, ownerId, kind, input.Attributes, input.OwnerExternallyVisible);
            var work = new Work(owner.Assembly, input, binding,
                [.. (constructor ? new[] { IrType.Reference(ownerId) } : Array.Empty<IrType>()),
                    .. input.Signature.Parameters.Select(t => Storage(t, parent, offset, borrow: true))],
                Storage(input.Signature.ReturnType, parent, offset, allowVoid: true),
                [.. input.Locals.Select(t => Storage(t, parent, offset))], parent, offset);
            if (constructor && work.Parameters.Any(t => t.Kind is IrTypeKind.Value or IrTypeKind.ManagedLocation))
                Fail(parent, "Class constructors require scalar/reference parameters; values and borrows need later lowering.", offset);
            _methods.Add(id, work); _pending.Enqueue(work); return work;
        }

        private IrType Storage(TypeSignature signature, Work? caller, int offset, bool borrow = false, bool allowVoid = false) => signature switch
        {
            TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 } => IrType.Int32,
            TypeSignature.Primitive { Code: PrimitiveTypeCode.Void } when allowVoid => IrType.Void,
            TypeSignature.Named { Kind: SignatureTypeKind.ValueType } value => IrType.Value(Include(value.Definition, caller, offset)),
            TypeSignature.Named { Kind: SignatureTypeKind.Class } reference when classes => IrType.Reference(IncludeReference(reference.Definition, caller, offset)),
            TypeSignature.Primitive { Code: PrimitiveTypeCode.Object } when classes => IrType.Reference(IncludeReference(
                Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Object), caller, offset), caller, offset)),
            TypeSignature.Vector { Element: TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 } } when arrays => IrType.Reference(IntegerArray()),
            TypeSignature.ByReference reference when borrow => IrType.Location(Storage(reference.Element, caller, offset)),
            _ => throw new Failure(new(DiagnosticIds.ExecutionSignatureUnsupported, DiagnosticSeverity.Error,
                "Initial value CIL signatures require Int32 or named nongeneric structs, void returns, and synchronous byref parameters. "
                + (classes ? "The object profile additionally accepts nominal classes/Object and Int32 vectors; other reference signatures"
                    : arrays ? "The array profile additionally accepts Int32 vectors; other reference signatures" : "Reference signatures")
                + ", modifiers, byref locals/returns and generics require later lowering." + Path(caller), Location(caller, offset))),
        };

        private ObjectTypeId Include(ResolvedType type, Work? work, int offset)
        {
            var id = ObjectModelBuilder.Id(type);
            if (!_values.ContainsKey(id) && _values.Count + _references.Count >= ObjectModelLimits.MaximumTypes)
                Fail(work, "Value type root limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            _values.TryAdd(id, type); return id;
        }

        private ObjectTypeId IncludeReference(ResolvedType type, Work? work, int offset)
        {
            var id = ObjectModelBuilder.Id(type);
            if (!_references.ContainsKey(id) && _values.Count + _references.Count >= ObjectModelLimits.MaximumTypes)
                Fail(work, "Combined value/reference root limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            _ = Snapshot(type, work, offset);
            _references.TryAdd(id, type); return id;
        }

        private bool Inline(ResolvedType type, Work? work, int offset)
        {
            var input = Snapshot(type, work, offset);
            _valueBase ??= ObjectModelBuilder.Id(Require(resolver.ResolveType(library, "System", "ValueType"), work, offset));
            return input.BaseType is TypeSignature.Named parent && ObjectModelBuilder.Id(parent.Definition) == _valueBase;
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
