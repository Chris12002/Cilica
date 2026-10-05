using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace Cilica.Compiler;

public static class ObjectIrValidator
{
    public static void Validate(ObjectIrProgram program) => Validate(program, locations: false);

    internal static void Validate(ObjectIrProgram program, bool locations, ScalarBoxingRules? boxes = null, bool delegates = false, ManagedExceptionRules? exceptions = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(program.Flow);
        ObjectModelValidator.Validate(program.Flow.Types);
        _ = new ObjectNativeLayout(program.Flow.Types);
        var rules = new ObjectIrRules(program, locations, delegates: delegates);
        ReferenceIrValidator.Validate(program.Flow, rules, locations ? new ManagedLocationRules(program.Flow.Types) : null, boxes, exceptions);
        rules.DispatchMaps();
        var entry = rules.Binding(program.Flow.Entry);
        var method = program.Flow.Methods.Single(m => IrMethodId.From(m.Source) == program.Flow.Entry);
        if (entry.Kind != ObjectMethodKind.Static || method.ReturnType != IrType.Int32 || method.Parameters.Any(p => p != IrType.Int32))
            throw new ArgumentException("Object program entry requires a static Int32 boundary; references stay internal.", nameof(program));
    }
}

internal sealed class ObjectIrRules
{
    private readonly Dictionary<IrMethodId, ObjectMethodBinding> _bindings = [];
    private readonly Dictionary<IrMethodId, IrMethod> _methods = [];
    private readonly Dictionary<ObjectFieldId, ObjectField> _fields;
    private readonly Dictionary<ObjectTypeId, ObjectTypeLayout> _layouts;
    private readonly HashSet<ObjectTypeId> _contracts;
    private readonly HashSet<ObjectTypeId> _arrays;
    private readonly IrTypeRules _types;
    private readonly ObjectDispatchRules _dispatch;
    private readonly ObjectInterfaceOperationRules _interfaces;
    private readonly ObjectIrProgram _program;
    private readonly bool _allowDelegates;
    private readonly Dictionary<ObjectTypeId, ObjectDelegateType> _delegates;
    private readonly HashSet<string> _literals = new(StringComparer.Ordinal);
    private int _literalUnits;

    internal ObjectIrRules(ObjectIrProgram program, bool locations = false, ValueTypeCatalog? values = null, bool delegates = false)
    {
        _program = program;
        var flow = program.Flow;
        _allowDelegates = delegates;
        Require(delegates || flow.Types.Delegates.IsEmpty, "delegate catalogs require the explicit delegate IR profile.");
        _delegates = flow.Types.Delegates.ToDictionary(d => ObjectTypeId.From(d.Source));
        _types = new(flow.Types, values, locations);
        _layouts = flow.Types.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
        _contracts = flow.Types.Interfaces.Select(t => ObjectTypeId.From(t.Source)).ToHashSet();
        _arrays = flow.Types.Arrays.ToHashSet();
        _fields = flow.Types.Types.SelectMany(t => t.Fields).ToDictionary(f => f.Id);
        Require(!flow.Methods.IsDefault && flow.Methods.Length is > 0 and <= ProgramLimits.MaximumMethods
            && !program.Bindings.IsDefault && program.Bindings.Length == flow.Methods.Length, "bounded methods and matching bindings are required.");
        foreach (var method in flow.Methods)
        {
            Require(method?.Source is not null && method.Source.ContentHash is { Length: 64 } && CanonicalHash(method.Source.ContentHash.ToLowerInvariant()), "invalid method source.");
            Require(_methods.TryAdd(IrMethodId.From(method.Source), method), "duplicate method identity.");
        }
        foreach (var binding in program.Bindings)
        {
            Require(binding?.Method is not null && binding.Owner is not null && binding.Owner.ArrayElement is null && ObjectModelValidator.Specialization(binding.Owner.Specialization) && CanonicalHash(binding.Owner.ContentHash)
                && (uint)binding.Owner.Token >> 24 == 0x02 && (binding.Owner.Token & 0xffffff) > 1
                && binding.Owner.ContentHash == binding.Method.ContentHash
                && _methods.ContainsKey(binding.Method) && _bindings.TryAdd(binding.Method, binding), "invalid or duplicate method binding identity.");
            Require(binding.Owner != flow.Types.String && !_delegates.ContainsKey(binding.Owner), "selected String methods require explicit intrinsic operations.");
            var method = _methods[binding.Method];
            Require(!method.Parameters.IsDefault && method.Parameters.Length <= IrLimits.MaximumParameters
                && method.Parameters.All(p => _types.Valid(p, true)) && (_types.Valid(method.ReturnType, true) || method.ReturnType == IrType.Void)
                && binding.Kind is ObjectMethodKind.Static or ObjectMethodKind.Instance
                or ObjectMethodKind.Constructor or ObjectMethodKind.ObjectBaseConstructor or ObjectMethodKind.ValueConstructor, "invalid bound signature/kind.");
            Require((binding.Attributes & ~(MethodAttributes.MemberAccessMask | MethodAttributes.Static | MethodAttributes.HideBySig
                | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Final)) == 0
                && (binding.Attributes & MethodAttributes.MemberAccessMask) is > MethodAttributes.PrivateScope and <= MethodAttributes.Public,
                "native, abstract and unsupported method flags require later lowering.");
            bool isStatic = binding.Kind == ObjectMethodKind.Static;
            bool valueConstructor = binding.Kind == ObjectMethodKind.ValueConstructor;
            Require(!method.Parameters.Any(p => p.Kind == IrTypeKind.ManagedLocation)
                || (isStatic || valueConstructor) && binding.VirtualSlot is null && (binding.Attributes & MethodAttributes.Virtual) == 0
                || flow.Types.Integer64 && binding.Kind == ObjectMethodKind.Instance,
                "borrowed parameters require static methods, value constructors or admitted integer-object instance methods.");
            Require(!binding.OwnerExternallyVisible || !_layouts.TryGetValue(binding.Owner, out var layout)
                || (layout.Attributes & TypeAttributes.VisibilityMask) is TypeAttributes.Public or TypeAttributes.NestedPublic,
                "owner visibility contradicts its layout attributes.");
            Require(((binding.Attributes & MethodAttributes.Static) != 0) == isStatic, "static attribute/kind mismatch.");
            if (valueConstructor)
                Require(values is not null && flow.Types.Integer64 && values.Types.Any(t => ObjectTypeId.From(t.Source) == binding.Owner
                        && (!binding.OwnerExternallyVisible || t.ExternallyVisible))
                    && method.Parameters.Length > 0 && method.Parameters[0] == IrType.Location(IrType.Value(binding.Owner))
                    && method.ReturnType == IrType.Void && binding.VirtualSlot is null
                    && (binding.Attributes & (MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Final))
                        == (MethodAttributes.SpecialName | MethodAttributes.RTSpecialName), "value constructor requires an exact writable inline receiver, void and constructor flags.");
            else if (!isStatic)
                Require(_layouts.ContainsKey(binding.Owner) && method.Parameters.Length > 0
                    && method.Parameters[0] == IrType.Reference(binding.Owner), "instance receiver must have the exact declaring class type.");
            if (Constructor(binding))
            {
                Require(method.ReturnType == IrType.Void && (binding.Attributes & (MethodAttributes.SpecialName | MethodAttributes.RTSpecialName))
                    == (MethodAttributes.SpecialName | MethodAttributes.RTSpecialName), "constructor requires void and constructor flags.");
                Require(binding.Kind == ObjectMethodKind.ObjectBaseConstructor ? binding.Owner == flow.Types.ObjectBase
                    && method.Parameters.Length == 1 : binding.Owner != flow.Types.ObjectBase, "invalid selected base constructor binding.");
            }
            else Require(binding.Owner != flow.Types.ObjectBase, "ordinary selected Object methods require explicit runtime support.");
        }
        _interfaces = new(program, _bindings, _methods, values);
        _dispatch = new(program, _bindings, _methods, _types, _interfaces);
    }

    internal void DispatchMaps() { _dispatch.Validate(); _interfaces.ValidateUsage(_program); }
    internal IEnumerable<IrMethodId> DispatchTargets(IrMethodId declaration) => _interfaces.Has(declaration)
        ? _interfaces.Targets(declaration) : _dispatch.Targets(declaration);
    internal bool Interface(IrMethodId declaration) => _interfaces.Has(declaration);
    internal IrType? ReturnType(IrMethodId? id) => id is null ? null : _interfaces.Has(id)
        ? _interfaces.Declaration(id).ReturnType : _methods.TryGetValue(id, out var method) ? method.ReturnType : null;

    internal ObjectMethodBinding Binding(IrMethodId id) => _bindings[id];
    internal bool InitializesValueField(IrMethod method, ObjectFieldId? field, IrExpression receiver) =>
        field is not null && Binding(IrMethodId.From(method.Source)) is { Kind: ObjectMethodKind.ValueConstructor } binding
        && binding.Owner == field.Owner && receiver is IrExpression.Parameter { Index: 0 };
    internal bool TypeTest(IrMethod method, ObjectTypeId? target) => target is { ArrayElement: not null }
        ? _program.Flow.Types.Integer64 && ArrayAllocation(method, target)
        : target is not null && (target == _program.Flow.Types.String || _contracts.Contains(target) || _layouts.TryGetValue(target, out var layout)
            && (Binding(IrMethodId.From(method.Source)).Owner == target
                || (layout.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public));
    internal IEnumerable<ObjectTypeId> OperationTypes(IrMethod method) => method.Blocks.SelectMany(b => b.Statements).SelectMany(s => s switch
    {
        IrStatement.Define { Value.Expression: IrExpression.LoadField f } => new[] { f.Field.Owner },
        IrStatement.Define { Value.Expression: IrExpression.AddressField f } => new[] { f.Field.Owner },
        IrStatement.StoreField f => new[] { f.Field.Owner },
        IrStatement.Define { Value.Expression: IrExpression.NewDelegate d } => new[] { d.Type },
        IrStatement.Define { Value.Expression: IrExpression.CallDelegate d } => new[] { d.Type },
        IrStatement.InvokeDelegate d => new[] { d.Type },
        IrStatement.Define { Value.Expression: IrExpression.NewArray a } => new[] { a.ArrayType },
        IrStatement.Define { Value.Expression: IrExpression.LoadElement a } => new[] { a.ArrayType },
        IrStatement.StoreElement a => new[] { a.ArrayType },
        IrStatement.Define { Value.Expression: IrExpression.StringLiteral or IrExpression.StringLength or IrExpression.StringCodeUnit
            or IrExpression.StringEquals or IrExpression.StringConcat } when _program.Flow.Types.String is { } text => new[] { text },
        IrStatement.Define { Value.Expression: IrExpression.Call c } when _interfaces.Has(c.Target) => _interfaces.SignatureTypes(c.Target),
        IrStatement.Invoke c when _interfaces.Has(c.Target) => _interfaces.SignatureTypes(c.Target),
        _ => Array.Empty<ObjectTypeId>(),
    });

    internal IrType? DelegateReturn(ObjectTypeId? type) => type is not null && _delegates.TryGetValue(type, out var declaration)
        ? declaration.ReturnsVoid ? IrType.Void : IrType.Int32 : null;

    internal bool DelegateCapture(IrMethod caller, IrExpression.NewDelegate capture, IrType result, IReadOnlyList<IrType> values)
    {
        if (!_allowDelegates || capture.Type is null || capture.Target is null || !_delegates.TryGetValue(capture.Type, out var declaration)
            || result != IrType.Reference(capture.Type) || capture.Receiver < 0 || capture.Receiver >= values.Count
            || !_bindings.TryGetValue(capture.Target, out var target) || target.Kind is not (ObjectMethodKind.Static or ObjectMethodKind.Instance)
            || (target.Attributes & MethodAttributes.Virtual) != 0 || target.VirtualSlot is not null
            || !Access(Binding(IrMethodId.From(caller.Source)), target)
            || !declaration.ExternallyVisible && Binding(IrMethodId.From(caller.Source)).Owner != capture.Type) return false;
        var body = _methods[capture.Target];
        int skip = target.Kind == ObjectMethodKind.Instance ? 1 : 0;
        return body.ReturnType == DelegateReturn(capture.Type) && body.Parameters.Length == declaration.ParameterCount + skip
            && body.Parameters.Skip(skip).All(p => p == IrType.Int32)
            && (skip == 0 ? values[capture.Receiver] == IrType.NullReference
                : _types.Assignable(values[capture.Receiver], IrType.Reference(target.Owner)));
    }

    internal bool DelegateCall(IrMethod caller, ObjectTypeId? type, int receiver,
        System.Collections.Immutable.ImmutableArray<int> arguments, IrType result, IReadOnlyList<IrType> values) =>
        _allowDelegates && type is not null && _delegates.TryGetValue(type, out var declaration)
        && (declaration.ExternallyVisible || Binding(IrMethodId.From(caller.Source)).Owner == type)
        && result == DelegateReturn(type) && receiver >= 0 && receiver < values.Count
        && _types.Assignable(values[receiver], IrType.Reference(type)) && !arguments.IsDefault
        && arguments.Length == declaration.ParameterCount
        && arguments.All(a => a >= 0 && a < values.Count && values[a] == IrType.Int32);

    internal IrType? StringType => _program.Flow.Types.String is { } text ? IrType.Reference(text) : null;

    internal bool StringOperand(int operand, IReadOnlyList<IrType> values) => StringType is { } type
        && operand >= 0 && operand < values.Count && _types.Assignable(values[operand], type);

    internal bool StringLiteral(System.Collections.Immutable.ImmutableArray<ushort> units)
    {
        if (StringType is null || units.IsDefault || units.Length > ObjectStringLimits.MaximumLiteralCodeUnits
            || units.Length > ObjectStringLimits.MaximumTotalLiteralCodeUnits - _literalUnits) return false;
        _literalUnits += units.Length;
        // The unit budget is checked before copying/hashing. Ordinal UTF-16 retains
        // unpaired surrogates, unlike an encoder that might replace invalid scalars.
        string content = string.Create(units.Length, units, static (target, source) =>
        {
            for (int i = 0; i < source.Length; i++) target[i] = (char)source[i];
        });
        _literals.Add(content);
        return _literals.Count <= ObjectStringLimits.MaximumDistinctLiterals;
    }

    internal IrType? ArrayElement(ObjectTypeId? array) => array is not null && _types.Valid(IrType.Reference(array), true) && _arrays.Contains(array)
        ? array.ArrayElement!.Type is { } element ? IrType.Reference(element) : IrType.Int32 : null;

    internal bool ArrayAllocation(IrMethod caller, ObjectTypeId? array) => ArrayElement(array) is not null
        && (array!.ArrayElement!.Type is not { } element || TypeTest(caller, element));

    internal bool ArrayLength(int array, IReadOnlyList<IrType> values) => array >= 0 && array < values.Count
        && (values[array] == IrType.NullReference || values[array].Class is { } type && _arrays.Contains(type));

    internal bool Element(ObjectTypeId? type, int array, int index, IReadOnlyList<IrType> values) => ArrayElement(type) is not null
        && array >= 0 && array < values.Count && index >= 0 && index < values.Count
        && _types.Assignable(values[array], IrType.Reference(type!)) && values[index] == IrType.Int32;

    internal bool StoreElement(IrStatement.StoreElement store, IReadOnlyList<IrType> values) =>
        Element(store.ArrayType, store.Array, store.Index, values) && store.Value >= 0 && store.Value < values.Count
        && _types.Assignable(values[store.Value], ArrayElement(store.ArrayType)) && store.Exceptional == IrExceptionTarget.MethodExit;

    internal bool Call(IrMethod caller, IrMethodId? target, System.Collections.Immutable.ImmutableArray<int> arguments,
        bool nullCheck, IReadOnlyList<IrType> values, bool allocation = false, bool virtualDispatch = false)
    {
        if (target is null || arguments.IsDefault) return false;
        if (_interfaces.Has(target))
        {
            var declaration = _interfaces.Declaration(target);
            return !allocation && nullCheck && virtualDispatch && arguments.Length == declaration.Parameters.Length
                && arguments.Select((argument, index) => argument >= 0 && argument < values.Count
                    && _types.Assignable(values[argument], declaration.Parameters[index])).All(valid => valid);
        }
        if (!_bindings.TryGetValue(target, out var callee)) return false;
        var signature = _methods[target].Parameters;
        int skip = allocation ? 1 : 0;
        if (signature.IsDefault || arguments.Length != signature.Length - skip || !Access(Binding(IrMethodId.From(caller.Source)), callee)) return false;
        if (allocation ? !Constructor(callee) : Constructor(callee)) return false;
        if (nullCheck && callee.Kind != ObjectMethodKind.Instance) return false;
        if (virtualDispatch ? allocation || !nullCheck || callee.VirtualSlot is null || !_dispatch.Has(target)
            : nullCheck && callee.VirtualSlot is not null) return false;
        for (int index = 0; index < arguments.Length; index++)
            if (arguments[index] < 0 || arguments[index] >= values.Count || !_types.Assignable(values[arguments[index]], signature[index + skip])) return false;
        return true;
    }

    internal bool BaseCall(IrMethod caller, IrStatement.Invoke call, IReadOnlyList<IrType> values, IReadOnlyList<IrExpression> expressions)
    {
        if (call.Target is null || !_bindings.TryGetValue(call.Target, out var target)) return false;
        var owner = Binding(IrMethodId.From(caller.Source));
        if (owner.Kind != ObjectMethodKind.Constructor || !Constructor(target)
            || target.Owner != _layouts[owner.Owner].BaseType || call.NullCheckReceiver || call.VirtualDispatch || call.Arguments.IsDefault
            || call.Arguments.Length != _methods[target.Method].Parameters.Length || call.Arguments.IsEmpty
            || call.Arguments[0] < 0 || call.Arguments[0] >= values.Count
            || expressions[call.Arguments[0]] is not IrExpression.Parameter { Index: 0 }
            || values[call.Arguments[0]] != IrType.Reference(owner.Owner) || !Access(owner, target)) return false;
        var signature = _methods[target.Method].Parameters;
        return call.Arguments.Select((argument, index) => argument >= 0 && argument < values.Count
            && _types.Assignable(values[argument], signature[index])).All(valid => valid);
    }

    internal IrType? AllocationType(IrMethodId? constructor) => constructor is not null && _bindings.TryGetValue(constructor, out var target)
        && Constructor(target) ? IrType.Reference(target.Owner) : null;

    internal bool Field(IrMethod method, ObjectFieldId? id, int receiver, IReadOnlyList<IrType> values, out IrType? storage)
    {
        storage = null;
        if (id?.Owner is not { ArrayElement: null } || !_fields.TryGetValue(id, out var field) || receiver < 0 || receiver >= values.Count
            || !_types.Assignable(values[receiver], IrType.Reference(id.Owner))) return false;
        var owner = Binding(IrMethodId.From(method.Source)).Owner;
        var access = field.Attributes & FieldAttributes.FieldAccessMask;
        if (access == FieldAttributes.PrivateScope || (owner != id.Owner && (access != FieldAttributes.Public
            || (_layouts[id.Owner].Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public))) return false;
        storage = field.Storage switch { ObjectStorage.Int32 => IrType.Int32, ObjectStorage.Int64 => IrType.Int64,
            ObjectStorage.Reference reference => IrType.Reference(reference.Type), _ => null! };
        return true;
    }

    internal bool Store(IrMethod method, IrStatement.StoreField store, IReadOnlyList<IrType> values, IReadOnlyList<IrExpression> expressions)
    {
        if (!Field(method, store.Field, store.Receiver, values, out var storage) || store.Value < 0 || store.Value >= values.Count
            || !_types.Assignable(values[store.Value], storage) || store.Exceptional != IrExceptionTarget.MethodExit) return false;
        if ((_fields[store.Field].Attributes & FieldAttributes.InitOnly) == 0) return true;
        var owner = Binding(IrMethodId.From(method.Source));
        return owner.Kind == ObjectMethodKind.Constructor && owner.Owner == store.Field.Owner
            && expressions[store.Receiver] is IrExpression.Parameter { Index: 0 };
    }

    internal void Method(IrMethod method)
    {
        var binding = Binding(IrMethodId.From(method.Source));
        string origin = " " + method.Source.Name + " token 0x" + method.Source.Token.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
        if (binding.Kind == ObjectMethodKind.Constructor)
        {
            void ConstructorRequire(bool condition, string message)
            {
                if (!condition) Require(false, message + origin);
            }
            ConstructorRequire(method.Blocks.Length == 1, "constructors require straight-line initialization.");
            var values = method.Blocks[0].Values;
            var types = values.Select(v => v.Type).ToArray();
            var expressions = values.Select(v => v.Expression).ToArray();
            bool initialized = false;
            bool This(int id) => expressions[id] is IrExpression.Parameter { Index: 0 };
            foreach (var statement in method.Blocks[0].Statements)
            {
                if (statement is IrStatement.Invoke call && _bindings.TryGetValue(call.Target, out var callee) && Constructor(callee))
                {
                    ConstructorRequire(!initialized && BaseCall(method, call, types, expressions)
                        && call.Arguments.Skip(1).All(a => !This(a)),
                        "constructor must call its immediate base once on its direct receiver.");
                    initialized = true;
                }
                else if (!initialized)
                {
                    // CLI permits declared-field initialization before the base call.
                    // Otherwise uninitialized this cannot be observed, aliased or escaped.
                    bool valid = statement switch
                    {
                        IrStatement.Define d => d.Value.Expression switch
                        {
                            IrExpression.Parameter or IrExpression.Constant or IrExpression.Null or IrExpression.Local => true,
                            IrExpression.Binary b => !This(b.Left) && !This(b.Right),
                            IrExpression.Unary u => !This(u.Operand),
                            IrExpression.Compare c => !This(c.Left) && !This(c.Right),
                            IrExpression.Call c => c.Arguments.All(a => !This(a)),
                            IrExpression.NewObject n => n.Arguments.All(a => !This(a)),
                            IrExpression.StringLiteral => true,
                            IrExpression.StringLength s => !This(s.Operand),
                            IrExpression.StringCodeUnit s => !This(s.Operand) && !This(s.Index),
                            IrExpression.StringEquals s => !This(s.Left) && !This(s.Right),
                            IrExpression.StringConcat s => !This(s.Left) && !This(s.Right),
                            IrExpression.NewArray a => !This(a.Length),
                            IrExpression.ArrayLength a => !This(a.Array),
                            IrExpression.LoadElement a => !This(a.Array) && !This(a.Index),
                            IrExpression.LoadField f => !This(f.Receiver),
                            IrExpression.CastClass c => !This(c.Operand),
                            IrExpression.IsInstance t => !This(t.Operand),
                            _ => false,
                        },
                        IrStatement.StoreLocal s => !This(s.Value),
                        IrStatement.Invoke c => c.Arguments.All(a => !This(a)),
                        IrStatement.StoreField s => !This(s.Value) && (!This(s.Receiver) || s.Field.Owner == binding.Owner),
                        IrStatement.StoreElement s => !This(s.Array) && !This(s.Index) && !This(s.Value),
                        _ => false,
                    };
                    ConstructorRequire(valid, "uninitialized constructor receiver cannot be read, aliased or passed before base initialization.");
                }
            }
            ConstructorRequire(initialized, "constructor must initialize its direct receiver through its immediate base constructor.");
        }
        if (binding.Kind == ObjectMethodKind.ObjectBaseConstructor)
            Require(method.Blocks.Length == 1 && method.Blocks[0].Statements.IsEmpty && method.Locals.IsEmpty
                && method.Blocks[0].Terminator is IrVoidReturn, "selected Object constructor must have an empty nop/ret body." + origin);
    }

    private static bool Constructor(ObjectMethodBinding b) => b.Kind is ObjectMethodKind.Constructor or ObjectMethodKind.ObjectBaseConstructor;
    private static bool Access(ObjectMethodBinding caller, ObjectMethodBinding target) => caller.Owner == target.Owner
        || (target.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public && target.OwnerExternallyVisible;
    private static bool CanonicalHash(string? hash) => hash is { Length: 64 } && hash.All(char.IsAsciiHexDigit) && hash == hash.ToLowerInvariant();
    private static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new ArgumentException("Invalid object operation IR: " + message);
    }
}
