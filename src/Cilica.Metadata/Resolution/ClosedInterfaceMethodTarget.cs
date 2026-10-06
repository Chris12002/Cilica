using System;
using System.Collections.Generic;
using System.Reflection.Metadata;

namespace Cilica.Metadata.Resolution;

/// <summary>Exact closed contract mapping on a concrete closed class; no body is executed or validated here.</summary>
public sealed record ClosedInterfaceMethodTarget(ResolvedMethod Declaration, ConstructedType Receiver,
    ResolvedMethod Mapping, ResolvedMethod? Slot, ResolvedMethod Implementation, bool IsExplicit)
{
    public const string Profile = "closed-interface-method-target-v2";
}

public sealed partial class AssemblyResolver
{
    /// <summary>Resolves an exact implemented contract, preserving closed owner arguments throughout mapping and slot selection.</summary>
    public ResolutionResult<ClosedInterfaceMethodTarget> ResolveClosedInterfaceMethodTarget(ResolvedMethod declaration,
        ConstructedType receiver, ResolvedAssembly runtimeLibrary, MemberResolutionBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(declaration); ArgumentNullException.ThrowIfNull(receiver);
        CheckOwner(receiver.Definition.Assembly); CheckOwner(runtimeLibrary);
        budget ??= new();
        var assembly = declaration.DeclaringType.Definition.Assembly;
        return Bind(assembly, declaration.Handle, () =>
        {
            var Validate = ClosedDispatchValidation(runtimeLibrary, budget);
            Validate(declaration.DeclaringType);
            var authenticated = Method(assembly, declaration.Handle,
                new(declaration.DeclaringType, SignatureList<TypeSignature>.Empty), declaredOnly: true, budget: budget);
            if (authenticated != declaration)
                throw new BadImageFormatException("Closed interface declaration must match its exact metadata owner and signature.");
            var target = InterfaceTarget(authenticated, receiver, budget, Validate);
            return new ClosedInterfaceMethodTarget(target.Declaration, receiver, target.Mapping,
                target.Slot, target.Implementation, target.IsExplicit);
        });
    }

    private Action<ConstructedType> ClosedDispatchValidation(ResolvedAssembly runtimeLibrary, MemberResolutionBudget budget)
    {
        var validated = new HashSet<ConstructedType>();
        void Validate(ConstructedType type)
        {
            CheckOwner(type.Definition.Assembly);
            // Check provenance before semantic identity deduplication.
            var pending = new Stack<TypeSignature>(type.Arguments); int nodes = 0;
            while (pending.TryPop(out var argument))
            {
                InspectMember(budget);
                if (++nodes > 4096) throw Failure(DiagnosticIds.TranslationLimit, "Closed dispatch arguments exceed 4096 nodes.");
                switch (argument)
                {
                    case TypeSignature.Named named: CheckOwner(named.Definition.Assembly); break;
                    case TypeSignature.Generic generic:
                        CheckOwner(generic.Definition.Definition.Assembly);
                        foreach (var child in generic.Arguments) pending.Push(child);
                        break;
                    case TypeSignature.Vector vector: pending.Push(vector.Element); break;
                }
            }
            if (!validated.Add(type)) return;
            if (validated.Count > 512) throw Failure(DiagnosticIds.TranslationLimit, "Closed dispatch type validation exceeds 512 declarations.");
            if (budget.Remaining == 0) throw Failure(DiagnosticIds.TranslationLimit, "Closed dispatch metadata budget exhausted.");
            var snapshot = ReadClosedTypeInput(type, runtimeLibrary, new(MaximumMembers: budget.Remaining));
            if (snapshot.Value is not { } value) throw new BindingFailureException(snapshot.Diagnostics);
            if (!budget.TryConsume(value.MetadataMembersRead)) throw Failure(DiagnosticIds.TranslationLimit, "Closed dispatch metadata budget exhausted.");
        }
        return Validate;
    }

}
