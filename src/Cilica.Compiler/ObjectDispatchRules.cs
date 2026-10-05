using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace Cilica.Compiler;

/// <summary>Checks detached slot assertions and complete class dispatch maps before native consumption.</summary>
internal sealed class ObjectDispatchRules
{
    private readonly ObjectIrProgram _program;
    private readonly IReadOnlyDictionary<IrMethodId, ObjectMethodBinding> _bindings;
    private readonly Dictionary<ObjectTypeId, ObjectTypeLayout> _layouts;
    private readonly Dictionary<IrMethodId, ObjectVirtualDispatch> _maps = [];
    private readonly Dictionary<(ObjectVirtualSlot Slot, ObjectTypeId Owner), IrMethodId> _bodies = [];
    private readonly IrTypeRules _types;
    private readonly ObjectInterfaceOperationRules _interfaces;

    internal ObjectDispatchRules(ObjectIrProgram program, IReadOnlyDictionary<IrMethodId, ObjectMethodBinding> bindings,
        IReadOnlyDictionary<IrMethodId, IrMethod> methods, IrTypeRules types, ObjectInterfaceOperationRules interfaces)
    {
        _program = program; _bindings = bindings; _types = types; _interfaces = interfaces;
        _layouts = program.Flow.Types.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
        var signatures = new Dictionary<ObjectVirtualSlot, ObjectMethodBinding>();
        foreach (var binding in bindings.Values)
        {
            if (binding.Kind == ObjectMethodKind.ValueInstance)
            {
                Require(binding.VirtualSlot is null && binding.VirtualName is null, "direct value methods cannot supply class dispatch slots.");
                continue;
            }
            bool isVirtual = (binding.Attributes & MethodAttributes.Virtual) != 0;
            Require(isVirtual == (binding.VirtualSlot is not null), "virtual flags and slot facts disagree.");
            if (!isVirtual)
            {
                Require(binding.VirtualName is null && (binding.Attributes & (MethodAttributes.NewSlot | MethodAttributes.Final)) == 0,
                    "nonvirtual methods cannot carry slot flags or names.");
                continue;
            }
            var signature = methods[binding.Method];
            Require(program.Flow.Types.Integer64 || !signature.Parameters.Append(signature.ReturnType)
                .Any(t => t.Kind is IrTypeKind.Value or IrTypeKind.ManagedLocation),
                "value/borrow virtual signatures require the explicit integer-object catalog.");
            Require(binding.Method.Specialization == binding.Owner.Specialization,
                "virtual methods must preserve their exact closed owner specialization.");
            var slot = binding.VirtualSlot!;
            Require(binding.Kind == ObjectMethodKind.Instance && ((binding.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public
                    || interfaces.Mapped(binding))
                && (binding.Attributes & MethodAttributes.RTSpecialName) == 0 && !string.IsNullOrEmpty(binding.VirtualName),
                "class virtual declarations require public instance methods or validated interface slots, with names.");
            Require(slot.Method is not null && slot.Owner is { ArrayElement: null } && _layouts.ContainsKey(slot.Owner)
                && slot.Method.ContentHash == slot.Owner.ContentHash && slot.Method.Specialization == slot.Owner.Specialization && (uint)slot.Method.Token >> 24 == 0x06 && (slot.Method.Token & 0xffffff) != 0
                && types.Assignable(IrType.Reference(binding.Owner), IrType.Reference(slot.Owner)), "invalid canonical virtual slot identity/owner.");
            if ((binding.Attributes & MethodAttributes.NewSlot) != 0)
                Require(slot.Method == binding.Method && slot.Owner == binding.Owner, "newslot must introduce its own canonical slot.");
            if (bindings.TryGetValue(slot.Method, out var introduction))
                Require(introduction.Owner == slot.Owner && introduction.VirtualSlot == slot,
                    "canonical slot contradicts its included declaration.");
            Require(_bodies.TryAdd((slot, binding.Owner), binding.Method), "duplicate virtual implementations on one slot owner.");
            if (signatures.TryGetValue(slot, out var previous))
            {
                var left = methods[previous.Method]; var right = methods[binding.Method];
                Require(previous.VirtualName == binding.VirtualName && left.ReturnType == right.ReturnType
                    && left.Parameters.Skip(1).SequenceEqual(right.Parameters.Skip(1)), "virtual slot names/signatures disagree.");
            }
            else signatures.Add(slot, binding);
        }
        Require(!program.Dispatch.IsDefault && program.Dispatch.Length <= ProgramLimits.MaximumMethods, "invalid dispatch map array.");
        int entries = interfaces.TargetCount;
        foreach (var map in program.Dispatch)
        {
            Require(map?.Declaration is not null && bindings.TryGetValue(map.Declaration, out var declaration)
                && declaration.VirtualSlot is not null && (declaration.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public
                && _maps.TryAdd(map.Declaration, map), "invalid/duplicate dispatch declaration.");
            Require(!map.Targets.IsDefault && map.Targets.Length is > 0 and <= ObjectModelLimits.MaximumTypes, "invalid dispatch targets.");
            entries += map.Targets.Length;
            Require(entries <= ObjectDispatchLimits.MaximumTargets, "aggregate dispatch target limit exceeded.");
            var receivers = new HashSet<ObjectTypeId>();
            foreach (var target in map.Targets)
                Require(target?.Receiver is { ArrayElement: null } && _layouts.ContainsKey(target.Receiver) && receivers.Add(target.Receiver)
                    && target.Implementation is not null && methods.ContainsKey(target.Implementation), "invalid/duplicate dispatch receiver or missing body.");
        }
    }

    internal bool Has(IrMethodId declaration) => _maps.ContainsKey(declaration);
    internal IEnumerable<IrMethodId> Targets(IrMethodId declaration) => _maps[declaration].Targets.Select(t => t.Implementation);

    internal void Validate()
    {
        var used = new HashSet<IrMethodId>();
        foreach (var statement in _program.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Statements))
            switch (statement)
            {
                case IrStatement.Define { Value.Expression: IrExpression.Call { VirtualDispatch: true } call } when !_interfaces.Has(call.Target): used.Add(call.Target); break;
                case IrStatement.Invoke { VirtualDispatch: true } call when !_interfaces.Has(call.Target): used.Add(call.Target); break;
            }
        Require(used.SetEquals(_maps.Keys), "dispatch maps must exactly match virtual call declarations.");
        var nearest = new Dictionary<(ObjectVirtualSlot Slot, ObjectTypeId Receiver), IrMethodId>();
        foreach (var map in _maps.Values)
        {
            var declaration = _bindings[map.Declaration]; var slot = declaration.VirtualSlot!;
            var expected = _layouts.Keys.Where(id => _types.Assignable(IrType.Reference(id), IrType.Reference(declaration.Owner))).ToHashSet();
            Require(expected.SetEquals(map.Targets.Select(t => t.Receiver)), "dispatch map must cover every compatible catalog receiver exactly once.");
            foreach (var target in map.Targets)
            {
                var key = (slot, target.Receiver);
                if (!nearest.TryGetValue(key, out var body))
                {
                    ObjectTypeId? owner = target.Receiver;
                    while (owner is not null && !_bodies.TryGetValue((slot, owner), out body)) owner = _layouts[owner].BaseType;
                    Require(owner is not null && body is not null, "receiver lacks an included virtual implementation.");
                    nearest.Add(key, body);
                }
                Require(target.Implementation == body, "dispatch target is not the nearest implementation of its canonical slot.");
                // A final implementation forbids reuse-slot replacements below it.
                bool final = false;
                var chain = new Stack<ObjectTypeId>();
                for (ObjectTypeId? id = target.Receiver; id is not null; id = _layouts[id].BaseType) chain.Push(id);
                while (chain.TryPop(out var id))
                    if (_bodies.TryGetValue((slot, id), out var implementation))
                    {
                        Require(!final, "dispatch override replaces a final implementation.");
                        final = (_bindings[implementation].Attributes & MethodAttributes.Final) != 0;
                    }
            }
        }
    }

    private static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new ArgumentException("Invalid object dispatch IR: " + message);
    }
}
