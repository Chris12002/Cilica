using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata.Analysis;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    /// <summary>
    /// Maps a CLI primitive through an explicitly selected library image. This does
    /// not normalize member signatures, retarget assemblies or probe a runtime.
    /// </summary>
    public ResolutionResult<ResolvedType> ResolveRuntimeType(ResolvedAssembly library, PrimitiveTypeCode code)
    {
        CheckOwner(library);
        var selected = ValidateRuntimeLibrary(library);
        if (!selected.Success) return ResolutionResult<ResolvedType>.Failed(selected.Diagnostics);
        string? name = code switch
        {
            PrimitiveTypeCode.Boolean => "Boolean", PrimitiveTypeCode.Char => "Char",
            PrimitiveTypeCode.SByte => "SByte", PrimitiveTypeCode.Byte => "Byte",
            PrimitiveTypeCode.Int16 => "Int16", PrimitiveTypeCode.UInt16 => "UInt16",
            PrimitiveTypeCode.Int32 => "Int32", PrimitiveTypeCode.UInt32 => "UInt32",
            PrimitiveTypeCode.Int64 => "Int64", PrimitiveTypeCode.UInt64 => "UInt64",
            PrimitiveTypeCode.Single => "Single", PrimitiveTypeCode.Double => "Double",
            PrimitiveTypeCode.IntPtr => "IntPtr", PrimitiveTypeCode.UIntPtr => "UIntPtr",
            PrimitiveTypeCode.Object => "Object", PrimitiveTypeCode.String => "String",
            _ => null,
        };
        if (name is null) return ResolutionResult<ResolvedType>.Failed(DiagnosticIds.ResolutionUnsupported,
            $"Primitive '{code}' has no receiver mapping in this profile.");
        var result = ResolveType(library, "System", name);
        if (result.Value is not ResolvedType type) return result;
        try
        {
            TypeDefinition definition = type.Assembly.Image.Reader.GetTypeDefinition(type.Handle);
            if (definition.GetGenericParameters().Count != 0 || (definition.Attributes & (TypeAttributes.Interface | TypeAttributes.Abstract)) != 0)
                return ResolutionResult<ResolvedType>.Failed(DiagnosticIds.ResolutionUnsupported,
                    $"Runtime mapping for System.{name} requires a nonabstract, nongeneric class/value-type definition.");
            return result;
        }
        catch (Exception exception) when (MalformedInput.IsMalformed(exception))
        {
            return ResolutionResult<ResolvedType>.Failed(DiagnosticIds.MalformedEntity, exception.Message, library.Identity.DisplayName);
        }
    }

    private ResolutionResult<ResolvedAssembly> ValidateRuntimeLibrary(ResolvedAssembly library)
    {
        // Selecting a path must not override a conflicting catalogue identity.
        if (!_candidates.ContainsKey(library.Identity)) return ResolutionResult<ResolvedAssembly>.Resolved(library);
        var result = ResolveAssembly(library.Identity);
        if (result.Value is not ResolvedAssembly candidate) return result;
        if (candidate.ContentHash != library.ContentHash)
            return ResolutionResult<ResolvedAssembly>.Failed(DiagnosticIds.AssemblyConflict,
                $"Selected runtime library '{library.Identity.DisplayName}' differs from the catalogue image " +
                $"(SHA-256: {library.ContentHash}, {candidate.ContentHash}). Supply one consistent profile.");
        return ResolutionResult<ResolvedAssembly>.Resolved(library);
    }

    private sealed partial class ReachabilityWalker
    {
        private readonly Dictionary<PrimitiveTypeCode, ResolutionResult<ResolvedType>> _runtimeTypes = [];
        private bool _runtimeLibraryUnavailable;

        private ConstructedType? PrimitiveReceiver(int id, PrimitiveTypeCode code, DependencyKind kind, int? offset, bool required)
        {
            if (_runtimeLibrary is null)
            {
                if (required) Issue(id, offset, DiagnosticIds.ReachabilityIncomplete,
                    "Primitive receiver mapping requires an explicitly selected runtime library (--runtime-library).");
                return null;
            }
            if (_runtimeLibraryUnavailable || _stopped) return null;
            if (!_runtimeTypes.TryGetValue(code, out var result))
            {
                result = _resolver.ResolveRuntimeType(_runtimeLibrary, code);
                _runtimeTypes.Add(code, result);
            }
            if (result.Value is not ResolvedType type)
            {
                foreach (Diagnostic diagnostic in result.Diagnostics) Issue(id, offset, diagnostic.Id, diagnostic.Message);
                return null;
            }
            var receiver = new ConstructedType(type, SignatureList<TypeSignature>.Empty);
            Add(new ReachableEntity.Type(receiver), id, kind, offset);
            return receiver;
        }

        private void StringReceiver(int id, int? offset)
        {
            // String signatures and ldstr can introduce receivers without a TypeDef
            // operand. Only an explicit library selection can supply that definition.
            if (PrimitiveReceiver(id, PrimitiveTypeCode.String, DependencyKind.RuntimeType, offset, required: false) is null)
                _hasSyntheticReceivers = true;
        }
    }
}
