using System;
using System.Reflection;
using Cilica.Metadata.Analysis;
using Cilica.Metadata.Formatting;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    /// <summary>Resolves the array base from a selected image, including explicit forwarders.</summary>
    public ResolutionResult<ResolvedType> ResolveRuntimeArrayBase(ResolvedAssembly library)
    {
        CheckOwner(library);
        var selected = ValidateRuntimeLibrary(library);
        if (!selected.Success) return ResolutionResult<ResolvedType>.Failed(selected.Diagnostics);
        var result = ResolveType(library, "System", "Array");
        if (result.Value is not ResolvedType type) return result;
        try
        {
            var definition = type.Assembly.Image.Reader.GetTypeDefinition(type.Handle);
            if (definition.GetGenericParameters().Count != 0
                || (definition.Attributes & (TypeAttributes.Abstract | TypeAttributes.Interface | TypeAttributes.Sealed)) != TypeAttributes.Abstract)
                return ResolutionResult<ResolvedType>.Failed(DiagnosticIds.ResolutionUnsupported,
                    "Runtime array base requires an abstract, nongeneric, nonsealed class definition.");
            return result;
        }
        catch (Exception exception) when (MalformedInput.IsMalformed(exception))
        {
            return ResolutionResult<ResolvedType>.Failed(DiagnosticIds.MalformedEntity, exception.Message, library.Identity.DisplayName);
        }
    }

    private sealed partial class ReachabilityWalker
    {
        private ResolutionResult<ResolvedType>? _arrayBase;

        private void ArrayReceiver(int id, TypeSignature shape, DependencyKind kind, int? offset)
        {
            if (ContainsVariable(shape) || _runtimeLibrary is null || _runtimeLibraryUnavailable)
            {
                _hasSyntheticReceivers = true;
                return;
            }
            _arrayBase ??= _resolver.ResolveRuntimeArrayBase(_runtimeLibrary);
            if (_arrayBase.Value is not ResolvedType type)
            {
                _hasSyntheticReceivers = true;
                foreach (Diagnostic diagnostic in _arrayBase.Diagnostics) Issue(id, offset, diagnostic.Id, diagnostic.Message);
                return;
            }
            Add(new ReachableEntity.ArrayReceiver(shape, new(type, SignatureList<TypeSignature>.Empty)), id, kind, offset);
        }

        private void DispatchArray(DispatchSite site, ReachableEntity.ArrayReceiver array, int receiverId)
        {
            // An array is concrete even though its metadata base System.Array is
            // abstract. Its inherited bodies use the structural array node as evidence.
            var hierarchy = Hierarchy(array.BaseType);
            var contract = site.Declaration.DeclaringType;
            if (site.Constraint is { } constraint && (TypeDefinition(constraint).Attributes & TypeAttributes.Interface) == 0
                && !hierarchy.Contains(constraint)) return;
            if (NeedsArrayInterfaceContract(contract, hierarchy)
                || site.Constraint is { } interfaceConstraint && NeedsArrayInterfaceContract(interfaceConstraint, hierarchy))
            {
                // Runtime-supplied generic interfaces have no normal MethodImpl
                // map on System.Array. Do not guess helper names or methods.
                Issue(site.Caller, site.Offset, DiagnosticIds.ReachabilityIncomplete,
                    "Array generic-interface dispatch requires an explicit runtime helper contract; array receiver node " + Invariant.Format(receiverId) + " remains unresolved.");
                return;
            }
            DispatchHierarchy(site, hierarchy, receiverId);
        }

        private bool NeedsArrayInterfaceContract(ConstructedType contract, System.Collections.Generic.List<ConstructedType> hierarchy) =>
            (TypeDefinition(contract).Attributes & TypeAttributes.Interface) != 0
            && contract.Arguments.Count > 0 && !Implements(hierarchy, contract);
    }
}
