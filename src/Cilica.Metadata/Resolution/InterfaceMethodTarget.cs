using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Cilica.Metadata.Resolution;

/// <summary>An exact interface mapping and effective class body; IL bodies are not verified or executed.</summary>
public sealed record InterfaceMethodTarget(ResolvedMethod Declaration, ResolvedType Receiver,
    ResolvedMethod Mapping, ResolvedMethod? Slot, ResolvedMethod Implementation, bool IsExplicit)
{
    public const string Profile = "interface-method-target-v1";
    public const int MaximumInterfaceTypes = 256;
}

public sealed partial class AssemblyResolver
{
    /// <summary>Selects one nongeneric abstract interface member on a concrete named class receiver.</summary>
    public ResolutionResult<InterfaceMethodTarget> ResolveInterfaceMethodTarget(ResolvedAssembly declarationAssembly,
        MethodDefinitionHandle declaration, ResolvedType receiver, MemberResolutionBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        CheckOwner(receiver.Assembly);
        budget ??= new();
        return Bind(declarationAssembly, declaration, () => InterfaceTarget(declarationAssembly, declaration, receiver, budget));
    }

    private InterfaceMethodTarget InterfaceTarget(ResolvedAssembly assembly, MethodDefinitionHandle handle,
        ResolvedType receiver, MemberResolutionBudget budget)
    {
        var declaration = Method(assembly, handle, declaredOnly: true, budget: budget);
        ValidateInterfaceMethod(declaration);
        var hierarchy = DispatchClassHierarchy(receiver, budget);
        var facts = ReadInterfaceHierarchy(hierarchy, budget, declaration.DeclaringType);
        var direct = facts.Direct; var mappings = facts.Mappings; var implemented = facts.Implemented;
        if (!implemented.Contains(declaration.DeclaringType))
            throw Failure(DiagnosticIds.MemberNotFound, "Receiver does not declare or inherit the requested interface contract.");

        ResolvedMethod? mapping = null; bool isExplicit = false;
        for (int i = hierarchy.Count - 1; i >= 0; i--)
        {
            var type = hierarchy[i];
            if (mappings[type].TryGetValue(declaration, out var explicitBody)) { mapping = explicitBody; isExplicit = true; }
            else if (direct[type].Contains(declaration.DeclaringType))
            {
                // Reimplementation starts a fresh search on this class; an
                // inherited mapping must not win over its public matching body.
                mapping = null;
                for (int search = i; search < hierarchy.Count && mapping is null; search++)
                {
                    var owner = hierarchy[search];
                    isExplicit = mappings[owner].TryGetValue(declaration, out mapping);
                    if (!isExplicit) mapping = InterfaceNameMatch(owner, declaration, budget);
                }
                if (mapping is null)
                    throw Failure(DiagnosticIds.MemberNotFound, "Concrete class has no implementation for the interface declaration; default bodies require later lowering.");
            }
        }
        if (mapping is null) throw Failure(DiagnosticIds.MemberNotFound, "Interface declaration has no supported class mapping.");
        if ((MethodFlags(mapping) & MethodAttributes.Virtual) == 0)
            return new(declaration, receiver, mapping, null, mapping, isExplicit);
        var target = ClassSlotTarget(mapping, receiver, hierarchy, budget, allowPrivateFinalSlots: isExplicit);
        return new(declaration, receiver, mapping, target.Slot, target.Implementation, isExplicit);
    }

    private sealed record InterfaceHierarchy(
        Dictionary<ConstructedType, HashSet<ConstructedType>> Direct,
        Dictionary<ConstructedType, Dictionary<ResolvedMethod, ResolvedMethod>> Mappings,
        HashSet<ConstructedType> Implemented);

    // Both exact resolvers authenticate every MethodImpl row against this same
    // bounded interface closure before the class-slot walk may consume it.
    private InterfaceHierarchy ReadInterfaceHierarchy(List<ConstructedType> hierarchy, MemberResolutionBudget budget,
        ConstructedType? requiredContract = null)
    {
        var direct = new Dictionary<ConstructedType, HashSet<ConstructedType>>();
        var graph = new Dictionary<ConstructedType, List<ConstructedType>>();
        var pending = new Queue<ConstructedType>(); var scheduled = new HashSet<ConstructedType>();
        void Schedule(ConstructedType type)
        {
            if (!scheduled.Add(type)) return;
            if (scheduled.Count > InterfaceMethodTarget.MaximumInterfaceTypes)
                throw Failure(DiagnosticIds.TranslationLimit, "Interface dispatch exceeds 256 distinct interface types.");
            pending.Enqueue(type);
        }
        if (requiredContract is not null) Schedule(requiredContract);
        foreach (var type in hierarchy)
        {
            var interfaces = ReadInterfaceEdges(type, budget);
            direct.Add(type, new(interfaces));
            foreach (var contract in interfaces) Schedule(contract);
        }
        while (pending.TryDequeue(out var type))
        {
            ValidateInterfaceType(type, budget);
            var parents = ReadInterfaceEdges(type, budget); graph.Add(type, parents);
            foreach (var parent in parents) Schedule(parent);
        }
        // Compute transitive interface sets from leaves upwards. This accepts
        // shared diamond ancestors without recursive walks or path cloning.
        var closure = new Dictionary<ConstructedType, HashSet<ConstructedType>>();
        var children = new Dictionary<ConstructedType, List<ConstructedType>>();
        var remaining = new Dictionary<ConstructedType, int>();
        var ready = new Queue<ConstructedType>();
        foreach (var pair in graph)
        {
            closure.Add(pair.Key, [pair.Key]); remaining.Add(pair.Key, pair.Value.Count);
            if (pair.Value.Count == 0) ready.Enqueue(pair.Key);
            foreach (var parent in pair.Value)
            {
                if (!children.TryGetValue(parent, out var list)) children.Add(parent, list = []);
                list.Add(pair.Key);
            }
        }
        int completed = 0;
        while (ready.TryDequeue(out var parent))
        {
            completed++;
            if (!children.TryGetValue(parent, out var dependents)) continue;
            foreach (var child in dependents)
            {
                closure[child].UnionWith(closure[parent]);
                if (--remaining[child] == 0) ready.Enqueue(child);
            }
        }
        if (completed != graph.Count) throw Failure(DiagnosticIds.TypeResolutionCycle, "Cyclic interface ancestry in exact dispatch.");
        foreach (var interfaces in direct.Values)
        {
            var roots = new List<ConstructedType>(interfaces);
            foreach (var root in roots) interfaces.UnionWith(closure[root]);
        }

        var mappings = new Dictionary<ConstructedType, Dictionary<ResolvedMethod, ResolvedMethod>>();
        var implemented = new HashSet<ConstructedType>();
        for (int i = hierarchy.Count - 1; i >= 0; i--)
        {
            var type = hierarchy[i]; implemented.UnionWith(direct[type]);
            mappings.Add(type, ReadInterfaceMappings(type, implemented, budget));
        }
        return new(direct, mappings, implemented);
    }

    private List<ConstructedType> ReadInterfaceEdges(ConstructedType type, MemberResolutionBudget budget)
    {
        var reader = type.Definition.Assembly.Image.Reader;
        var result = new List<ConstructedType>(); var seen = new HashSet<ConstructedType>();
        foreach (var edge in reader.GetTypeDefinition(type.Definition.Handle).GetInterfaceImplementations())
        {
            InspectMember(budget);
            if (result.Count >= InterfaceMethodTarget.MaximumInterfaceTypes)
                throw Failure(DiagnosticIds.TranslationLimit, "Interface declaration exceeds 256 direct interfaces.");
            var parent = Owner(type.Definition.Assembly, reader.GetInterfaceImplementation(edge).Interface,
                new(type, SignatureList<TypeSignature>.Empty));
            if (!seen.Add(parent)) throw new BadImageFormatException("Duplicate direct interface declaration.");
            result.Add(parent);
        }
        return result;
    }

    private static void ValidateInterfaceMethod(ResolvedMethod method)
    {
        var owner = method.DeclaringType;
        var type = owner.Definition.Assembly.Image.Reader.GetTypeDefinition(owner.Definition.Handle);
        const MethodAttributes required = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Abstract;
        const MethodAttributes allowed = required | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.SpecialName;
        var flags = MethodFlags(method);
        if ((type.Attributes & (TypeAttributes.Interface | TypeAttributes.Abstract)) != (TypeAttributes.Interface | TypeAttributes.Abstract)
            || (flags & required) != required || (flags & ~allowed) != 0 || method.Signature.Header != 0x20
            || method.Signature.GenericArity != 0 || owner.Arguments.Count != 0 || method.MethodArguments.Count != 0)
            throw new UnsupportedBindingException("Exact interface dispatch requires public nongeneric abstract instance interface declarations; default/static members require later lowering.");
    }

    private void ValidateInterfaceType(ConstructedType owner, MemberResolutionBudget budget)
    {
        var type = owner.Definition.Assembly.Image.Reader.GetTypeDefinition(owner.Definition.Handle);
        if (MetadataTokens.GetRowNumber(owner.Definition.Handle) == 1 || owner.Arguments.Count != 0
            || (type.Attributes & (TypeAttributes.Interface | TypeAttributes.Abstract | TypeAttributes.Sealed)) != (TypeAttributes.Interface | TypeAttributes.Abstract)
            || (type.Attributes & TypeAttributes.VisibilityMask) is not (TypeAttributes.Public or TypeAttributes.NestedPublic)
            || !type.BaseType.IsNil || type.GetMethodImplementations().Count != 0)
            throw new UnsupportedBindingException("Interface dispatch requires public nongeneric abstract interfaces without default remapping or class bases.");
        InspectOwner(owner.Definition, budget);
        for (var enclosing = owner.Definition.Handle; !enclosing.IsNil;)
        {
            InspectMember(budget);
            var definition = owner.Definition.Assembly.Image.Reader.GetTypeDefinition(enclosing);
            if (definition.GetGenericParameters().Count != 0)
                throw new UnsupportedBindingException("Generic interface declaring owners require later lowering.");
            enclosing = definition.GetDeclaringType();
            if ((definition.Attributes & TypeAttributes.VisibilityMask) != (enclosing.IsNil ? TypeAttributes.Public : TypeAttributes.NestedPublic))
                throw new UnsupportedBindingException("Interface dispatch requires public interfaces with public enclosing types.");
        }
        foreach (var field in type.GetFields())
        {
            InspectMember(budget);
            if ((owner.Definition.Assembly.Image.Reader.GetFieldDefinition(field).Attributes & FieldAttributes.Static) == 0)
                throw new BadImageFormatException("Interfaces cannot declare instance fields.");
        }
        var declarations = new HashSet<(string Name, MethodSignature Signature)>();
        foreach (var method in type.GetMethods())
        {
            InspectMember(budget);
            var declaration = Method(owner.Definition.Assembly, method, declaredOnly: true, budget: budget);
            ValidateInterfaceMethod(declaration);
            string name = owner.Definition.Assembly.Image.Reader.GetString(owner.Definition.Assembly.Image.Reader.GetMethodDefinition(method).Name);
            if (name.Length == 0 || !declarations.Add((name, declaration.Signature)))
                throw Failure(DiagnosticIds.MemberConflict, "Empty or duplicate interface member name/signature.");
        }
    }

    private Dictionary<ResolvedMethod, ResolvedMethod> ReadInterfaceMappings(ConstructedType type,
        HashSet<ConstructedType> implemented, MemberResolutionBudget budget)
    {
        var reader = type.Definition.Assembly.Image.Reader;
        var result = new Dictionary<ResolvedMethod, ResolvedMethod>();
        foreach (var handle in reader.GetTypeDefinition(type.Definition.Handle).GetMethodImplementations())
        {
            InspectMember(budget);
            var row = reader.GetMethodImplementation(handle);
            var declaration = Method(type.Definition.Assembly, row.MethodDeclaration, declaredOnly: true, budget: budget);
            if ((declaration.DeclaringType.Definition.Assembly.Image.Reader.GetTypeDefinition(declaration.DeclaringType.Definition.Handle).Attributes & TypeAttributes.Interface) == 0)
                throw new UnsupportedBindingException("Class MethodImpl slot remapping requires later lowering; only interface declarations are supported.");
            ValidateInterfaceMethod(declaration);
            if (!implemented.Contains(declaration.DeclaringType)) throw new BadImageFormatException("Explicit mapping refers to an unimplemented interface.");
            var body = Method(type.Definition.Assembly, row.MethodBody, declaredOnly: true, budget: budget);
            ValidateClassVirtual(body, allowPrivateFinalSlots: true);
            if (body.DeclaringType != type || body.Signature != declaration.Signature)
                throw new UnsupportedBindingException("Explicit interface body requires the implementing owner and an identical signature.");
            if (!result.TryAdd(declaration, body)) throw new BadImageFormatException("Duplicate explicit mappings for one interface declaration.");
        }
        return result;
    }

    private ResolvedMethod? InterfaceNameMatch(ConstructedType owner, ResolvedMethod declaration, MemberResolutionBudget budget)
    {
        var reader = owner.Definition.Assembly.Image.Reader;
        string name = declaration.DeclaringType.Definition.Assembly.Image.Reader.GetString(
            declaration.DeclaringType.Definition.Assembly.Image.Reader.GetMethodDefinition(declaration.Handle).Name);
        ResolvedMethod? match = null;
        foreach (var handle in reader.GetTypeDefinition(owner.Definition.Handle).GetMethods())
        {
            InspectMember(budget);
            var flags = reader.GetMethodDefinition(handle).Attributes;
            if (!reader.StringComparer.Equals(reader.GetMethodDefinition(handle).Name, name)
                || (flags & MethodAttributes.MemberAccessMask) != MethodAttributes.Public || (flags & MethodAttributes.Static) != 0
                || reader.GetMethodDefinition(handle).GetGenericParameters().Count != 0) continue;
            var candidate = Method(owner.Definition.Assembly, handle, declaredOnly: true, budget: budget);
            if (candidate.Signature != declaration.Signature)
            {
                if (candidate.Signature.Header == declaration.Signature.Header && candidate.Signature.Parameters.Equals(declaration.Signature.Parameters))
                    throw new UnsupportedBindingException("Differing interface return signatures require later lowering.");
                continue;
            }
            if (match is not null) throw Failure(DiagnosticIds.MemberConflict, "Multiple public methods match one interface name/signature on an owner.");
            const MethodAttributes allowed = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName
                | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Final;
            if ((flags & ~allowed) != 0 || (flags & MethodAttributes.Virtual) == 0 && (flags & (MethodAttributes.NewSlot | MethodAttributes.Final)) != 0)
                throw new UnsupportedBindingException("Implicit interface bodies require ordinary concrete public instance methods.");
            match = candidate;
        }
        return match;
    }

    private static MethodAttributes MethodFlags(ResolvedMethod method) =>
        method.DeclaringType.Definition.Assembly.Image.Reader.GetMethodDefinition(method.Handle).Attributes;
}
