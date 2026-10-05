using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

/// <summary>Checks detached declaration, mapping and effective-slot assertions; it does not authenticate metadata or IL.</summary>
public static class ObjectInterfacePlanValidator
{
    public static void Validate(ObjectInterfaceDispatchPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ObjectModelValidator.Validate(plan.Types, inlineFields: plan.Values is not null && plan.Types.Integer64);
        if (plan.Values is { } values)
        {
            _ = ValueLayoutPlanner.Build(values);
            Require(plan.Types.Integer64 && ObjectInterfaceOperationRules.CatalogMatches(plan.Types, values.References),
                "interface value signatures require the same explicit integer-object catalog.");
        }
        var types = new IrTypeRules(plan.Types, plan.Values, locations: plan.Types.Integer64);
        var classes = plan.Types.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
        var contracts = plan.Types.Interfaces.ToDictionary(t => ObjectTypeId.From(t.Source));
        Require(!plan.Declarations.IsDefault && plan.Declarations.Length is > 0 and <= ProgramLimits.MaximumMethods
            && !plan.Methods.IsDefault && plan.Declarations.Length + plan.Methods.Length <= ProgramLimits.MaximumMethods,
            "declarations and class method facts share the 256-method limit.");
        bool Signature(System.Collections.Immutable.ImmutableArray<IrType> parameters, IrType result, ObjectTypeId owner) =>
            !parameters.IsDefault && parameters.Length is > 0 and <= IrLimits.MaximumParameters
            && parameters[0] == IrType.Reference(owner) && parameters.All(t => types.Valid(t, true))
            && result is not null && (result.Kind != IrTypeKind.ManagedLocation && types.Valid(result, true) || result == IrType.Void);
        var declarations = new Dictionary<IrMethodId, ObjectInterfaceDeclaration>();
        int parameters = 0;
        foreach (var declaration in plan.Declarations)
        {
            Require(declaration?.Source is not null && Identity(declaration.Owner, 0x02) && contracts.ContainsKey(declaration.Owner)
                && declaration.Source.ContentHash == declaration.Owner.ContentHash
                && declaration.Source.Specialization == declaration.Owner.Specialization
                && Identity(IrMethodId.From(declaration.Source), 0x06) && declaration.Source.AssemblyIdentity is not null
                && declaration.Source.Name is not null && !string.IsNullOrEmpty(declaration.Name)
                && declaration.OwnerExternallyVisible, "invalid interface declaration provenance/owner/name.");
            const MethodAttributes required = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Abstract;
            const MethodAttributes allowed = required | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.SpecialName;
            Require((declaration.Attributes & required) == required && (declaration.Attributes & ~allowed) == 0
                && Signature(declaration.Parameters, declaration.ReturnType, declaration.Owner), "unsupported interface flags/signature.");
            Require(declarations.TryAdd(IrMethodId.From(declaration.Source), declaration), "duplicate interface declaration identity.");
            parameters += declaration.Parameters.Length;
            Require(parameters <= ProgramLimits.MaximumCallArguments, "aggregate declaration parameter limit exceeded.");
        }
        var names = new HashSet<(ObjectTypeId, string, IrType, SignatureList<IrType>)>();
        foreach (var declaration in declarations.Values)
            Require(names.Add((declaration.Owner, declaration.Name, declaration.ReturnType, new(declaration.Parameters.Skip(1)))),
                "duplicate interface member name/signature.");
        var methods = new Dictionary<IrMethodId, ObjectDispatchMethod>();
        var bodies = new Dictionary<(ObjectVirtualSlot, ObjectTypeId), ObjectDispatchMethod>();
        foreach (var method in plan.Methods)
        {
            if (method is { ArrayHelper: true })
            {
                var helper = method.Binding;
                Require(plan.Types.Integer64 && helper is not null && Identity(helper.Owner, 0x02)
                    && Identity(helper.Method, 0x06) && helper.Owner.ContentHash == plan.Types.ObjectBase.ContentHash
                    && helper.Method.ContentHash == helper.Owner.ContentHash && helper.Method.Specialization == helper.Owner.Specialization
                    && helper.Owner.Specialization.Length == 64 && helper.Kind == ObjectMethodKind.Static
                    && helper.VirtualSlot is null && helper.VirtualName is null && helper.Primitive is null && !helper.InvariantProviderOnly
                    && helper.Attributes == (MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig)
                    && !helper.OwnerExternallyVisible && helper.OwnerAssemblyVisible
                    && !method.Parameters.IsDefaultOrEmpty && method.Parameters[0].Class is { ArrayElement: not null } vector
                    && plan.Types.Arrays.Contains(vector) && Signature(method.Parameters, method.ReturnType, vector),
                    "invalid selected array helper facts/identity/signature.");
                var element = method.Parameters[0].Class!.ArrayElement!;
                Require(method.Name == "Count" && method.Parameters.Length == 1 && method.ReturnType == IrType.Int32
                    || method.Name == "Get" && method.Parameters.Length == 2 && method.Parameters[1] == IrType.Int32
                        && method.ReturnType == (element.Type is { } reference ? IrType.Reference(reference) : IrType.Int32),
                    "unsupported array helper operation/signature.");
                Require(methods.TryAdd(helper.Method, method), "duplicate array helper identity.");
                parameters += method.Parameters.Length;
                Require(parameters <= ProgramLimits.MaximumCallArguments, "aggregate declaration parameter limit exceeded.");
                continue;
            }
            Require(method?.Binding is not null && method.Binding.Method is not null && Identity(method.Binding.Owner, 0x02)
                && classes.ContainsKey(method.Binding.Owner) && method.Binding.Method.ContentHash == method.Binding.Owner.ContentHash
                && method.Binding.Method.Specialization == method.Binding.Owner.Specialization
                && Identity(method.Binding.Method, 0x06) && !declarations.ContainsKey(method.Binding.Method)
                && !string.IsNullOrEmpty(method.Name) && method.Binding.Kind == ObjectMethodKind.Instance
                && Signature(method.Parameters, method.ReturnType, method.Binding.Owner), "invalid class method facts/identity/signature.");
            var binding = method.Binding; bool virtualMethod = (binding.Attributes & MethodAttributes.Virtual) != 0;
            var access = binding.Attributes & MethodAttributes.MemberAccessMask;
            const MethodAttributes allowed = MethodAttributes.MemberAccessMask | MethodAttributes.HideBySig | MethodAttributes.SpecialName
                | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Final;
            bool privateSlot = access == MethodAttributes.Private && virtualMethod
                && (binding.Attributes & (MethodAttributes.Final | MethodAttributes.NewSlot)) == (MethodAttributes.Final | MethodAttributes.NewSlot);
            Require((binding.Attributes & ~allowed) == 0 && (access == MethodAttributes.Public || privateSlot)
                && virtualMethod == (binding.VirtualSlot is not null), "mapping bodies require public instance methods or private final newslots.");
            if (binding.OwnerExternallyVisible)
                Require((classes[binding.Owner].Attributes & TypeAttributes.VisibilityMask) is TypeAttributes.Public or TypeAttributes.NestedPublic,
                    "class owner visibility contradicts its layout.");
            if (virtualMethod)
            {
                var slot = binding.VirtualSlot!;
                Require(slot.Method is not null && Identity(slot.Method, 0x06) && Identity(slot.Owner, 0x02)
                    && classes.ContainsKey(slot.Owner) && slot.Method.ContentHash == slot.Owner.ContentHash
                    && types.Assignable(IrType.Reference(binding.Owner), IrType.Reference(slot.Owner)) && binding.VirtualName == method.Name,
                    "invalid canonical slot/owner/name.");
                if ((binding.Attributes & MethodAttributes.NewSlot) != 0)
                    Require(slot.Method == binding.Method && slot.Owner == binding.Owner, "newslot must introduce itself.");
                Require(bodies.TryAdd((slot, binding.Owner), method), "duplicate slot body on one class owner.");
            }
            else Require(binding.VirtualName is null && (binding.Attributes & (MethodAttributes.NewSlot | MethodAttributes.Final)) == 0,
                "nonvirtual methods cannot carry slot facts.");
            Require(methods.TryAdd(binding.Method, method), "duplicate class method identity.");
            parameters += method.Parameters.Length;
            Require(parameters <= ProgramLimits.MaximumCallArguments, "aggregate declaration parameter limit exceeded.");
        }
        Require(parameters <= ProgramLimits.MaximumCallArguments, "aggregate declaration parameter limit exceeded.");
        foreach (var method in methods.Values.Where(m => m.Binding.VirtualSlot is not null))
        {
            var slot = method.Binding.VirtualSlot!;
            Require(methods.TryGetValue(slot.Method, out var introduction) && introduction.Binding.Owner == slot.Owner
                && introduction.Binding.VirtualSlot == slot && SameSignature(method, introduction) && method.Name == introduction.Name,
                "missing or contradictory canonical slot declaration.");
        }
        Require(!plan.Dispatch.IsDefault && plan.Dispatch.Length == declarations.Count, "one map per requested interface declaration is required.");
        var maps = new HashSet<IrMethodId>(); var used = new HashSet<IrMethodId>(); int targets = 0;
        foreach (var map in plan.Dispatch)
        {
            Require(map?.Declaration is not null && declarations.ContainsKey(map.Declaration)
                && maps.Add(map.Declaration) && !map.Targets.IsDefault && map.Targets.Length <= ObjectModelLimits.MaximumTypes,
                "invalid/duplicate interface map.");
            var declaration = declarations[map.Declaration];
            var expected = classes.Keys.Concat(plan.Types.Arrays).Where(id => types.Assignable(IrType.Reference(id), IrType.Reference(declaration.Owner))).ToHashSet();
            var receivers = new Dictionary<ObjectTypeId, ObjectInterfaceTarget>();
            foreach (var target in map.Targets)
            {
                Require(target?.Receiver is not null && expected.Contains(target.Receiver) && receivers.TryAdd(target.Receiver, target)
                    && target.Mapping is not null && methods.ContainsKey(target.Mapping)
                    && target.Implementation is not null && methods.ContainsKey(target.Implementation), "invalid/duplicate receiver or missing method facts.");
                var mapping = methods[target.Mapping]; var implementation = methods[target.Implementation];
                if (target.Receiver.ArrayElement is not null)
                {
                    var kind = contracts[declaration.Owner].ArrayContract;
                    Require(mapping.ArrayHelper && target.Mapping == target.Implementation && !target.IsExplicit
                        && mapping.Parameters[0] == IrType.Reference(target.Receiver)
                        && declaration.Parameters.Skip(1).SequenceEqual(mapping.Parameters.Skip(1))
                        && types.Assignable(mapping.ReturnType, declaration.ReturnType)
                        && (mapping.Name == "Count" && declaration.Name == "get_Count"
                                && kind is ObjectArrayInterfaceKind.Collection or ObjectArrayInterfaceKind.ReadOnlyCollection
                            || mapping.Name == "Get" && declaration.Name == "get_Item"
                                && kind is ObjectArrayInterfaceKind.List or ObjectArrayInterfaceKind.ReadOnlyList),
                        "array mapping requires the exact selected helper and matching interface operation.");
                    used.Add(target.Implementation); continue;
                }
                Require(!mapping.ArrayHelper && !implementation.ArrayHelper, "class mapping cannot use array helpers.");
                Require(types.Assignable(IrType.Reference(target.Receiver), IrType.Reference(mapping.Binding.Owner))
                    && declaration.ReturnType == mapping.ReturnType && declaration.Parameters.Skip(1).SequenceEqual(mapping.Parameters.Skip(1)),
                    "mapping owner/signature does not implement its contract.");
                Require(target.IsExplicit ? mapping.Binding.VirtualSlot is not null
                    : (mapping.Binding.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public && mapping.Name == declaration.Name,
                    "explicit/implicit mapping facts contradict the selected method.");
                var slot = mapping.Binding.VirtualSlot;
                if (slot is null) Require(target.Mapping == target.Implementation, "nonvirtual mappings retain their original body.");
                else
                {
                    ObjectTypeId? owner = target.Receiver; ObjectDispatchMethod? nearest = null;
                    while (owner is not null && !bodies.TryGetValue((slot, owner), out nearest)) owner = classes[owner].BaseType;
                    Require(nearest is not null && nearest.Binding.Method == target.Implementation && SameSignature(mapping, implementation),
                        "effective body is not the nearest implementation of the mapping's canonical slot.");
                    bool final = false; var chain = new Stack<ObjectTypeId>();
                    for (ObjectTypeId? id = target.Receiver; id is not null; id = classes[id].BaseType) chain.Push(id);
                    while (chain.TryPop(out var id)) if (bodies.TryGetValue((slot, id), out var body))
                    {
                        Require(!final, "interface target overrides a final class implementation.");
                        final = (body.Binding.Attributes & MethodAttributes.Final) != 0;
                    }
                }
                used.Add(target.Mapping); used.Add(target.Implementation);
            }
            Require(expected.SetEquals(receivers.Keys), "map must cover every compatible catalog class exactly once.");
            foreach (var target in map.Targets.Where(t => t.Receiver.ArrayElement is null))
            {
                var receiver = classes[target.Receiver];
                bool reimplements = receiver.Interfaces.Any(id => types.Assignable(IrType.Reference(id), IrType.Reference(declaration.Owner)));
                bool newExplicit = target.IsExplicit && methods[target.Mapping].Binding.Owner == target.Receiver;
                if (!reimplements && !newExplicit && receiver.BaseType is { } parent && receivers.TryGetValue(parent, out var inherited))
                    Require(target.Mapping == inherited.Mapping && target.IsExplicit == inherited.IsExplicit,
                        "inherited interface mapping cannot change through class method hiding.");
            }
            targets += map.Targets.Length;
            Require(targets <= ObjectDispatchLimits.MaximumTargets, "aggregate interface target limit exceeded.");
        }
        foreach (var method in methods.Values)
            if (used.Contains(method.Binding.Method) && method.Binding.VirtualSlot is { } slot) used.Add(slot.Method);
        Require(used.SetEquals(methods.Keys), "plan contains unrelated class method facts.");
    }

    private static bool SameSignature(ObjectDispatchMethod left, ObjectDispatchMethod right) =>
        left.ReturnType == right.ReturnType && left.Parameters.Skip(1).SequenceEqual(right.Parameters.Skip(1));
    private static bool Identity(ObjectTypeId? id, uint table) => id is { ArrayElement: null } && ObjectModelValidator.Specialization(id.Specialization) && Token(id.ContentHash, id.Token, table);
    private static bool Identity(IrMethodId? id, uint table) => id is not null && ObjectModelValidator.Specialization(id.Specialization) && Token(id.ContentHash, id.Token, table);
    private static bool Token(string? hash, int token, uint table) => hash is { Length: 64 } && hash.All(char.IsAsciiHexDigit)
        && hash == hash.ToLowerInvariant() && (uint)token >> 24 == table && (token & 0xffffff) > (table == 0x02 ? 1 : 0);
    private static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new ArgumentException("Invalid interface dispatch plan: " + message);
    }
}
