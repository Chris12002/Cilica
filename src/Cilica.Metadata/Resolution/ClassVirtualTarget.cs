using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Cilica.Metadata.Resolution;

/// <summary>One exact class slot choice. Method bodies and executable compatibility are not validated here.</summary>
public sealed record ClassVirtualTarget(ResolvedMethod Declaration, ResolvedMethod Slot,
    ResolvedType Receiver, ResolvedMethod Implementation)
{
    public const string Profile = "class-virtual-target-v2";
    public const int MaximumHierarchyDepth = 256;
}

public sealed partial class AssemblyResolver
{
    /// <summary>
    /// Selects a public nongeneric class virtual slot for one concrete named receiver.
    /// Unlike conservative reachability, success identifies exactly one implementation.
    /// Interface-only MethodImpl rows are validated without changing class slots.
    /// Interface/abstract/generic hierarchies, class slot remapping and differing return
    /// signatures require later profiles. No inspected method is executed.
    /// </summary>
    public ResolutionResult<ClassVirtualTarget> ResolveClassVirtualTarget(ResolvedAssembly declarationAssembly,
        MethodDefinitionHandle declaration, ResolvedType receiver, MemberResolutionBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        // Reader ownership must be checked before semantic identity deduplication.
        CheckOwner(receiver.Assembly);
        budget ??= new();
        return Bind(declarationAssembly, declaration, () => ClassTarget(declarationAssembly, declaration, receiver, budget));
    }

    private ClassVirtualTarget ClassTarget(ResolvedAssembly assembly, MethodDefinitionHandle handle,
        ResolvedType receiver, MemberResolutionBudget budget)
    {
        var declaration = Method(assembly, handle, declaredOnly: true, budget: budget);
        ValidateClassVirtual(declaration);
        var hierarchy = DispatchClassHierarchy(receiver, budget);
        if (hierarchy.Exists(t => t.Definition.Assembly.Image.Reader.GetTypeDefinition(t.Definition.Handle).GetMethodImplementations().Count != 0))
            _ = ReadInterfaceHierarchy(hierarchy, budget);
        return ClassSlotTarget(declaration, receiver, hierarchy, budget);
    }

    private List<ConstructedType> DispatchClassHierarchy(ResolvedType receiver, MemberResolutionBudget budget)
        => DispatchClassHierarchy(OpenType(receiver), budget);

    private List<ConstructedType> DispatchClassHierarchy(ConstructedType receiver, MemberResolutionBudget budget,
        Action<ConstructedType>? validateClosed = null)
    {
        var hierarchy = new List<ConstructedType>();
        var seen = new HashSet<ResolvedType>();
        var current = receiver;
        while (true)
        {
            InspectMember(budget);
            validateClosed?.Invoke(current);
            if (!seen.Add(current.Definition) || seen.Count > ClassVirtualTarget.MaximumHierarchyDepth)
                throw Failure(DiagnosticIds.TypeResolutionCycle, "Class virtual hierarchy is cyclic or exceeds 256 types.");
            var type = current.Definition.Assembly.Image.Reader.GetTypeDefinition(current.Definition.Handle);
            if (MetadataTokens.GetRowNumber(current.Definition.Handle) == 1)
                throw new BadImageFormatException("The module definition is not a class dispatch receiver or ancestor.");
            if ((type.Attributes & TypeAttributes.Interface) != 0
                || validateClosed is null && (current.Arguments.Count != 0 || (type.Attributes & TypeAttributes.Abstract) != 0))
                throw new UnsupportedBindingException("Exact class dispatch requires class ancestors; abstract/generic ancestry requires the explicit closed dispatch profile.");
            if (hierarchy.Count > 0 && (type.Attributes & TypeAttributes.Sealed) != 0)
                throw new BadImageFormatException("Class virtual hierarchy extends a sealed class.");
            var owners = new HashSet<TypeDefinitionHandle>();
            for (var owner = current.Definition.Handle; !owner.IsNil;)
            {
                InspectMember(budget);
                if (!owners.Add(owner) || owners.Count > TypeReadLimits.MaximumSupportedOwnerDepth)
                    throw Failure(DiagnosticIds.TranslationLimit, "Class dispatch declaring owners are cyclic or exceed 64 levels.");
                var enclosing = current.Definition.Assembly.Image.Reader.GetTypeDefinition(owner);
                if (validateClosed is null && enclosing.GetGenericParameters().Count != 0)
                    throw new UnsupportedBindingException("Generic declaring owners require a later class dispatch profile.");
                owner = enclosing.GetDeclaringType();
            }
            hierarchy.Add(current);
            if (!BaseTypes(current, current, out var parent, out _)) break;
            current = parent;
        }
        return hierarchy;
    }

    private ClassVirtualTarget ClassSlotTarget(ResolvedMethod declaration, ResolvedType receiver,
        List<ConstructedType> hierarchy, MemberResolutionBudget budget, bool allowPrivateFinalSlots = false, bool closed = false)
    {
        ValidateClassVirtual(declaration, allowPrivateFinalSlots, closed);
        var assembly = declaration.DeclaringType.Definition.Assembly;
        string name = assembly.Image.Reader.GetString(assembly.Image.Reader.GetMethodDefinition(declaration.Handle).Name);
        if (!hierarchy.Exists(t => t == declaration.DeclaringType))
            throw Failure(DiagnosticIds.MemberNotFound, "Virtual declaration owner is outside the receiver's class hierarchy.");

        // Each newslot starts an independent chain. Reuse-slot definitions replace
        // only the nearest matching chain, leaving older hidden slots untouched.
        var slots = new List<(ResolvedMethod Root, ResolvedMethod Body)>();
        int selected = -1;
        for (int index = hierarchy.Count - 1; index >= 0; index--)
        {
            var owner = hierarchy[index];
            var image = owner.Definition.Assembly;
            var metadata = image.Image.Reader;
            ResolvedMethod? match = null;
            int exactDeclarations = 0;
            foreach (var candidateHandle in metadata.GetTypeDefinition(owner.Definition.Handle).GetMethods())
            {
                InspectMember(budget);
                var definition = metadata.GetMethodDefinition(candidateHandle);
                if (!metadata.StringComparer.Equals(definition.Name, name)) continue;
                // Match before substituting method arguments, including generic overload arity.
                if (definition.GetGenericParameters().Count != declaration.Signature.GenericArity) continue;
                var candidate = Method(image, candidateHandle, closed ? new(owner, SignatureList<TypeSignature>.Empty) : null, declaredOnly: true, budget: budget);
                if (candidate.Signature != declaration.Signature)
                {
                    if ((definition.Attributes & MethodAttributes.Virtual) != 0 && candidate.Signature.Header == declaration.Signature.Header
                        && candidate.Signature.Parameters.Equals(declaration.Signature.Parameters))
                        throw new UnsupportedBindingException("Differing virtual return signatures, including covariance, require a later dispatch profile.");
                    continue;
                }
                if (++exactDeclarations > 1)
                    throw Failure(DiagnosticIds.MemberConflict, "Multiple class declarations match one virtual name/signature on an owner.");
                if ((definition.Attributes & MethodAttributes.Virtual) == 0) continue;
                ValidateClassVirtual(candidate, allowPrivateFinalSlots, closed);
                match = candidate;
            }
            if (match is null) continue;
            var flags = metadata.GetMethodDefinition(match.Handle).Attributes;
            if (slots.Count == 0 || (flags & MethodAttributes.NewSlot) != 0) slots.Add((match, match));
            else
            {
                var previous = slots[^1];
                if ((previous.Body.DeclaringType.Definition.Assembly.Image.Reader.GetMethodDefinition(previous.Body.Handle).Attributes & MethodAttributes.Final) != 0)
                    throw new BadImageFormatException("Class dispatch override attempts to replace a final virtual method.");
                slots[^1] = (previous.Root, match);
            }
            if (match.DeclaringType == declaration.DeclaringType && match.Handle == declaration.Handle) selected = slots.Count - 1;
        }
        if (selected < 0) throw Failure(DiagnosticIds.MemberNotFound, "Virtual declaration was not found in its owner's method list.");
        var selectedBody = slots[selected].Body;
        if (closed && (receiver.Assembly.Image.Reader.GetTypeDefinition(receiver.Handle).Attributes & TypeAttributes.Abstract) == 0
            && (selectedBody.DeclaringType.Definition.Assembly.Image.Reader.GetMethodDefinition(selectedBody.Handle).Attributes & MethodAttributes.Abstract) != 0)
            throw new UnsupportedBindingException("Concrete class receiver lacks an implementation of its abstract virtual slot.");
        return new(declaration, slots[selected].Root, receiver, selectedBody);
    }

    private static void ValidateClassVirtual(ResolvedMethod method, bool allowPrivateFinalSlots = false, bool closed = false)
    {
        var definition = method.DeclaringType.Definition.Assembly.Image.Reader.GetMethodDefinition(method.Handle);
        const MethodAttributes allowed = MethodAttributes.MemberAccessMask | MethodAttributes.Virtual
            | MethodAttributes.NewSlot | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.SpecialName;
        bool privateFinalSlot = allowPrivateFinalSlots
            && (definition.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Private
            && (definition.Attributes & (MethodAttributes.Final | MethodAttributes.NewSlot)) == (MethodAttributes.Final | MethodAttributes.NewSlot);
        bool isAbstract = (definition.Attributes & MethodAttributes.Abstract) != 0;
        if (isAbstract && (!closed || definition.RelativeVirtualAddress != 0 || definition.ImplAttributes != MethodImplAttributes.IL
            || (definition.Attributes & MethodAttributes.Final) != 0
            || (method.DeclaringType.Definition.Assembly.Image.Reader.GetTypeDefinition(method.DeclaringType.Definition.Handle).Attributes & TypeAttributes.Abstract) == 0))
            throw new UnsupportedBindingException("Abstract class slots require a bodyless managed IL declaration on an abstract owner and cannot be final.");
        bool genericFinalSlot = closed && method.Signature.GenericArity > 0
            && method.MethodArguments.Count == method.Signature.GenericArity && method.Signature.Header == 0x30
            && (definition.Attributes & (MethodAttributes.Final | MethodAttributes.NewSlot | MethodAttributes.Abstract))
                == (MethodAttributes.Final | MethodAttributes.NewSlot);
        var admitted = allowed | (closed ? MethodAttributes.Abstract : 0);
        if ((definition.Attributes & MethodAttributes.Virtual) == 0 || (definition.Attributes & ~admitted) != 0
            || (definition.Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public && !privateFinalSlot
            || !closed && method.DeclaringType.Arguments.Count != 0
            || !genericFinalSlot && (method.MethodArguments.Count != 0 || method.Signature.Header != 0x20 || method.Signature.GenericArity != 0))
            throw new UnsupportedBindingException(allowPrivateFinalSlots
                ? "Exact interface mapping bodies require public virtual or private final newslot instance declarations; generic methods require final newslots without abstract or special runtime semantics."
                : "Exact class dispatch requires public nongeneric instance virtual declarations without abstract or special runtime semantics.");
    }
}
