using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    private const int MaximumTypeResolutionDepth = 256;

    /// <summary>
    /// Resolves a top-level type and optional nested names, preserving namespace/name
    /// segments instead of matching formatted strings. Follows outer-type forwarders.
    /// </summary>
    public ResolutionResult<ResolvedType> ResolveType(ResolvedAssembly assembly, string @namespace, params string[] names)
    {
        CheckOwner(assembly);
        ArgumentNullException.ThrowIfNull(@namespace);
        ArgumentNullException.ThrowIfNull(names);
        if (names.Length == 0 || names.Any(string.IsNullOrEmpty))
            throw new ArgumentException("At least one nonempty type name is required.", nameof(names));
        if (names.Length > MaximumTypeResolutionDepth)
            return ResolutionResult<ResolvedType>.Failed(DiagnosticIds.TypeResolutionCycle,
                "Type nesting exceeds the resolver limit of 256 levels.", assembly.Identity.DisplayName);
        try
        {
            ResolutionResult<ResolvedType> result = ResolveTopLevel(assembly, @namespace, names[0]);
            for (int index = 1; index < names.Length && result.Value is ResolvedType parent; index++)
            {
                TypeIndex types = Index(parent.Assembly);
                types.Definitions.TryGetValue((parent.Handle, "", names[index]), out var matches);
                result = TypeMatch(parent.Assembly, matches, string.Join("/", names.Take(index + 1)));
            }
            return result;
        }
        catch (Exception exception) when (MalformedInput.IsMalformed(exception) || exception is UnsupportedBindingException)
        {
            return TypeFailure(assembly, exception);
        }
    }

    /// <summary>
    /// Resolves a TypeDef or TypeRef handle scoped to an owned image. Generic/array TypeSpecs
    /// require signature resolution in a later block; they are diagnosed, never guessed.
    /// </summary>
    public ResolutionResult<ResolvedType> ResolveType(ResolvedAssembly assembly, EntityHandle handle)
    {
        CheckOwner(assembly);
        try
        {
            if (handle.Kind == HandleKind.TypeDefinition)
            {
                int row = MetadataTokens.GetRowNumber(handle);
                if (row == 0 || row > assembly.Image.Reader.TypeDefinitions.Count)
                    throw new BadImageFormatException("Type definition row does not exist.");
                return ResolutionResult<ResolvedType>.Resolved(new ResolvedType(assembly, (TypeDefinitionHandle)handle));
            }
            if (handle.Kind != HandleKind.TypeReference)
                return ResolutionResult<ResolvedType>.Failed(DiagnosticIds.ResolutionUnsupported,
                    $"Expected a TypeDef or TypeRef; {handle.Kind} requires a different resolution operation.", assembly.Identity.DisplayName);
            var visited = new HashSet<EntityHandle>();
            var names = new List<string>();
            string @namespace = "";
            MetadataReader reader = assembly.Image.Reader;
            while (handle.Kind == HandleKind.TypeReference)
            {
                if (!visited.Add(handle) || visited.Count > MaximumTypeResolutionDepth)
                    return ResolutionResult<ResolvedType>.Failed(DiagnosticIds.TypeResolutionCycle,
                        "Type reference scopes are cyclic or exceed 256 levels.", assembly.Identity.DisplayName);
                int row = MetadataTokens.GetRowNumber(handle);
                if (row == 0 || row > reader.TypeReferences.Count)
                    throw new BadImageFormatException("Type reference row does not exist.");
                TypeReference reference = reader.GetTypeReference((TypeReferenceHandle)handle);
                names.Add(reader.GetString(reference.Name));
                @namespace = reader.GetString(reference.Namespace);
                handle = reference.ResolutionScope;
            }
            names.Reverse();
            if (names.Any(string.IsNullOrEmpty)) throw new BadImageFormatException("Type reference has an empty name.");
            ResolvedAssembly target = assembly;
            if (handle.Kind == HandleKind.AssemblyReference && !handle.IsNil)
            {
                ResolutionResult<ResolvedAssembly> resolved = ResolveAssembly(AssemblyIdentity.ReadReference(reader, (AssemblyReferenceHandle)handle));
                if (resolved.Value is null)
                    return ResolutionResult<ResolvedType>.Failed(resolved.Diagnostics.Select(d => d with
                    {
                        Location = $"type reference {@namespace}.{string.Join("/", names)} in {assembly.Identity.DisplayName}",
                    }));
                target = resolved.Value;
            }
            else if (handle.Kind != HandleKind.ModuleDefinition || handle.IsNil)
            {
                return ResolutionResult<ResolvedType>.Failed(DiagnosticIds.ResolutionUnsupported,
                    "Only current-module and assembly-scoped type references are supported; multi-module/nil scopes require an explicit profile.", assembly.Identity.DisplayName);
            }
            return ResolveType(target, @namespace, names.ToArray());
        }
        catch (Exception exception) when (MalformedInput.IsMalformed(exception) || exception is UnsupportedBindingException)
        {
            return TypeFailure(assembly, exception);
        }
    }

    private ResolutionResult<ResolvedType> ResolveTopLevel(ResolvedAssembly assembly, string @namespace, string name)
    {
        var visited = new HashSet<ResolvedAssembly>();
        var chain = new List<string>();
        string typeName = @namespace.Length == 0 ? name : @namespace + "." + name;
        while (true)
        {
            chain.Add(assembly.Identity.DisplayName);
            if (!visited.Add(assembly) || visited.Count > MaximumTypeResolutionDepth)
                return ResolutionResult<ResolvedType>.Failed(DiagnosticIds.TypeResolutionCycle,
                    $"Forwarding '{typeName}' is cyclic or exceeds 256 hops: {string.Join(" -> ", chain)}.");
            TypeIndex types = Index(assembly);
            types.Definitions.TryGetValue((default, @namespace, name), out var definitions);
            types.Exports.TryGetValue((@namespace, name), out var exports);
            int definitionCount = definitions?.Count ?? 0;
            int exportCount = exports?.Count ?? 0;
            if (definitionCount + exportCount > 1)
                return ResolutionResult<ResolvedType>.Failed(DiagnosticIds.TypeConflict,
                    $"Type '{typeName}' has conflicting definitions or exports.", assembly.Identity.DisplayName);
            if (definitionCount > 0 || exportCount == 0)
            {
                ResolutionResult<ResolvedType> result = TypeMatch(assembly, definitions, typeName);
                if (result.Success || chain.Count == 1) return result;
                return ResolutionResult<ResolvedType>.Failed(result.Diagnostics.Select(d => d with
                {
                    Location = $"forwarder {typeName}: {string.Join(" -> ", chain)}",
                }));
            }
            ExportedType exported = assembly.Image.Reader.GetExportedType(exports![0]);
            if (!exported.IsForwarder || exported.Implementation.Kind != HandleKind.AssemblyReference || exported.Implementation.IsNil)
                return ResolutionResult<ResolvedType>.Failed(DiagnosticIds.ResolutionUnsupported,
                    $"Export '{typeName}' is not an assembly type forwarder; multi-module exports are unsupported.", assembly.Identity.DisplayName);
            AssemblyIdentity target = AssemblyIdentity.ReadReference(assembly.Image.Reader, (AssemblyReferenceHandle)exported.Implementation);
            ResolutionResult<ResolvedAssembly> resolved = ResolveAssembly(target);
            if (resolved.Value is null)
                return ResolutionResult<ResolvedType>.Failed(resolved.Diagnostics.Select(d => d with
                {
                    Location = $"forwarder {typeName}: {string.Join(" -> ", chain)}",
                }));
            assembly = resolved.Value;
        }
    }

    private static TypeIndex Index(ResolvedAssembly assembly) =>
        assembly.Types ??= new TypeIndex(assembly.Image.Reader);

    private static ResolutionResult<ResolvedType> TypeMatch(ResolvedAssembly assembly,
        List<TypeDefinitionHandle>? matches, string name) => matches?.Count switch
    {
        1 => ResolutionResult<ResolvedType>.Resolved(new ResolvedType(assembly, matches[0])),
        > 1 => ResolutionResult<ResolvedType>.Failed(DiagnosticIds.TypeConflict,
            $"Type '{name}' has multiple definitions.", assembly.Identity.DisplayName),
        _ => ResolutionResult<ResolvedType>.Failed(DiagnosticIds.TypeNotFound,
            $"Type '{name}' was not found.", assembly.Identity.DisplayName),
    };

    private static ResolutionResult<ResolvedType> TypeFailure(ResolvedAssembly assembly, Exception exception) =>
        ResolutionResult<ResolvedType>.Failed(
            exception is UnsupportedBindingException ? DiagnosticIds.ResolutionUnsupported : DiagnosticIds.ReferenceImageInvalid,
            $"Cannot resolve type metadata: {exception.Message}", assembly.Identity.DisplayName);
}
