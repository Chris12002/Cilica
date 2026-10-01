using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace Cilica.Compiler;

public static class ObjectIrValidator
{
    public static void Validate(ObjectIrProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(program.Flow);
        ObjectModelValidator.Validate(program.Flow.Types);
        var rules = new ObjectIrRules(program);
        ReferenceIrValidator.Validate(program.Flow, rules);
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
    private readonly IrTypeRules _types;

    internal ObjectIrRules(ObjectIrProgram program)
    {
        var flow = program.Flow;
        _types = new(flow.Types);
        _layouts = flow.Types.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
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
            Require(binding?.Method is not null && binding.Owner is not null && CanonicalHash(binding.Owner.ContentHash)
                && (uint)binding.Owner.Token >> 24 == 0x02 && (binding.Owner.Token & 0xffffff) > 1
                && binding.Owner.ContentHash == binding.Method.ContentHash
                && _methods.ContainsKey(binding.Method) && _bindings.TryAdd(binding.Method, binding), "invalid or duplicate method binding identity.");
            var method = _methods[binding.Method];
            Require(!method.Parameters.IsDefault && method.Parameters.Length <= IrLimits.MaximumParameters && binding.Kind is ObjectMethodKind.Static or ObjectMethodKind.Instance
                or ObjectMethodKind.Constructor or ObjectMethodKind.ObjectBaseConstructor, "invalid bound signature/kind.");
            Require((binding.Attributes & ~(MethodAttributes.MemberAccessMask | MethodAttributes.Static | MethodAttributes.HideBySig
                | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)) == 0
                && (binding.Attributes & MethodAttributes.MemberAccessMask) is > MethodAttributes.PrivateScope and <= MethodAttributes.Public,
                "virtual, native, abstract and unsupported method flags require later lowering.");
            bool isStatic = binding.Kind == ObjectMethodKind.Static;
            Require(!binding.OwnerExternallyVisible || !_layouts.TryGetValue(binding.Owner, out var layout)
                || (layout.Attributes & TypeAttributes.VisibilityMask) is TypeAttributes.Public or TypeAttributes.NestedPublic,
                "owner visibility contradicts its layout attributes.");
            Require(((binding.Attributes & MethodAttributes.Static) != 0) == isStatic, "static attribute/kind mismatch.");
            if (!isStatic)
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
    }

    internal ObjectMethodBinding Binding(IrMethodId id) => _bindings[id];
    internal IEnumerable<ObjectTypeId> OperationTypes(IrMethod method) => method.Blocks.SelectMany(b => b.Statements).SelectMany(s => s switch
    {
        IrStatement.Define { Value.Expression: IrExpression.LoadField f } => new[] { f.Field.Owner },
        IrStatement.StoreField f => new[] { f.Field.Owner },
        _ => Array.Empty<ObjectTypeId>(),
    });

    internal bool Call(IrMethod caller, IrMethodId? target, System.Collections.Immutable.ImmutableArray<int> arguments,
        bool nullCheck, IReadOnlyList<IrType> values, bool allocation = false)
    {
        if (target is null || !_bindings.TryGetValue(target, out var callee) || arguments.IsDefault) return false;
        var signature = _methods[target].Parameters;
        int skip = allocation ? 1 : 0;
        if (signature.IsDefault || arguments.Length != signature.Length - skip || !Access(Binding(IrMethodId.From(caller.Source)), callee)) return false;
        if (allocation ? !Constructor(callee) : Constructor(callee)) return false;
        if (nullCheck && callee.Kind != ObjectMethodKind.Instance) return false;
        for (int index = 0; index < arguments.Length; index++)
            if (arguments[index] < 0 || arguments[index] >= values.Count || !_types.Assignable(values[arguments[index]], signature[index + skip])) return false;
        return true;
    }

    internal bool BaseCall(IrMethod caller, IrStatement.Invoke call, IReadOnlyList<IrType> values, IReadOnlyList<IrExpression> expressions)
    {
        if (call.Target is null || !_bindings.TryGetValue(call.Target, out var target)) return false;
        var owner = Binding(IrMethodId.From(caller.Source));
        return owner.Kind == ObjectMethodKind.Constructor && target.Kind == ObjectMethodKind.ObjectBaseConstructor
            && !call.NullCheckReceiver && !call.Arguments.IsDefault && call.Arguments.Length == 1
            && call.Arguments[0] >= 0 && call.Arguments[0] < values.Count
            && expressions[call.Arguments[0]] is IrExpression.Parameter { Index: 0 }
            && values[call.Arguments[0]] == IrType.Reference(owner.Owner) && Access(owner, target);
    }

    internal IrType? AllocationType(IrMethodId? constructor) => constructor is not null && _bindings.TryGetValue(constructor, out var target)
        && Constructor(target) ? IrType.Reference(target.Owner) : null;

    internal bool Field(IrMethod method, ObjectFieldId? id, int receiver, IReadOnlyList<IrType> values, out IrType? storage)
    {
        storage = null;
        if (id is null || !_fields.TryGetValue(id, out var field) || receiver < 0 || receiver >= values.Count
            || !_types.Assignable(values[receiver], IrType.Reference(id.Owner))) return false;
        var owner = Binding(IrMethodId.From(method.Source)).Owner;
        var access = field.Attributes & FieldAttributes.FieldAccessMask;
        if (access == FieldAttributes.PrivateScope || (owner != id.Owner && (access != FieldAttributes.Public
            || (_layouts[id.Owner].Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public))) return false;
        storage = field.Storage is ObjectStorage.Int32 ? IrType.Int32 : IrType.Reference(((ObjectStorage.Reference)field.Storage).Type);
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
            Require(method.Blocks.Length == 1 && method.Blocks[0].Statements.Length >= 2
                && method.Blocks[0].Statements[0] is IrStatement.Define { Value.Expression: IrExpression.Parameter { Index: 0 } }
                && method.Blocks[0].Statements[1] is IrStatement.Invoke call
                && BaseCall(method, call, [IrType.Reference(binding.Owner)], [new IrExpression.Parameter(0)]),
                "constructor must initialize its direct receiver through the selected Object constructor before other operations." + origin);
            Require(method.Blocks[0].Statements.Skip(2).OfType<IrStatement.Invoke>().All(c => !_bindings.TryGetValue(c.Target, out var target) || !Constructor(target)),
                "constructor base initialization cannot be repeated." + origin);
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
