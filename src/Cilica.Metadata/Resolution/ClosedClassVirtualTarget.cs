using System;

namespace Cilica.Metadata.Resolution;

/// <summary>One exact closed class slot choice; executable compatibility is checked separately.</summary>
public sealed record ClosedClassVirtualTarget(ResolvedMethod Declaration, ResolvedMethod Slot,
    ConstructedType Receiver, ResolvedMethod Implementation)
{
    public const string Profile = "closed-class-virtual-target-v5";
}

public sealed partial class AssemblyResolver
{
    public ResolutionResult<ClosedClassVirtualTarget> ResolveClosedClassVirtualTarget(ResolvedMethod declaration,
        ConstructedType receiver, ResolvedAssembly runtimeLibrary, MemberResolutionBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(declaration); ArgumentNullException.ThrowIfNull(receiver);
        CheckOwner(receiver.Definition.Assembly); CheckOwner(runtimeLibrary);
        budget ??= new();
        var assembly = declaration.DeclaringType.Definition.Assembly;
        return Bind(assembly, declaration.Handle, () =>
        {
            var validate = ClosedDispatchValidation(runtimeLibrary, budget);
            validate(declaration.DeclaringType);
            var authenticated = Method(assembly, declaration.Handle,
                new(declaration.DeclaringType, SignatureList<TypeSignature>.Empty), declaredOnly: true, budget: budget);
            if (authenticated.Signature.GenericArity != 0 || authenticated.MethodArguments.Count != 0)
                throw new UnsupportedBindingException("Generic class calls require later dispatch lowering; only closed generic interface final slots are admitted.");
            if (authenticated != declaration)
                throw new BadImageFormatException("Closed class declaration must match its exact metadata owner and signature.");
            ValidateClassVirtual(authenticated, closed: true, allowAssemblySlots: true, allowFamilySlots: true);
            var hierarchy = DispatchClassHierarchy(receiver, budget, validate);
            if (hierarchy.Exists(t => t.Definition.Assembly.Image.Reader.GetTypeDefinition(t.Definition.Handle).GetMethodImplementations().Count != 0))
                _ = ReadInterfaceHierarchy(hierarchy, budget, validateClosed: validate);
            var target = ClassSlotTarget(authenticated, receiver.Definition, hierarchy, budget, closed: true, allowAssemblySlots: true, allowFamilySlots: true);
            return new ClosedClassVirtualTarget(target.Declaration, target.Slot, receiver, target.Implementation);
        });
    }
}
