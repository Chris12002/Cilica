using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    /// <summary>
    /// Validates explicit reference-to-implementation type pairs without mutating
    /// strict resolution. All selected images must be owned, unambiguous snapshots.
    /// This establishes identity/header/base correspondence, not executable compatibility.
    /// </summary>
    public ResolutionResult<LibraryBindingPlan> CreateLibraryBindingPlan(IEnumerable<LibraryTypeMapping> mappings,
        MemberResolutionBudget? budget = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(mappings);
        budget ??= new();
        try
        {
            var pairs = new Dictionary<ResolvedType, ResolvedType>();
            var images = new Dictionary<AssemblyIdentity, string>();
            int count = 0;
            foreach (var mapping in mappings)
            {
                if (++count > LibraryBindingPlan.MaximumMappings)
                    throw Failure(DiagnosticIds.TranslationLimit, "Library binding selection exceeds 1024 entries, including duplicates.");
                ArgumentNullException.ThrowIfNull(mapping);
                ArgumentNullException.ThrowIfNull(mapping.Contract);
                ArgumentNullException.ThrowIfNull(mapping.Implementation);
                CheckOwner(mapping.Contract.Assembly); CheckOwner(mapping.Implementation.Assembly);
                InspectMember(budget);
                if (!mapping.Contract.Assembly.IsReferenceAssembly || mapping.Implementation.Assembly.IsReferenceAssembly)
                    throw InvalidLibraryBinding("Mappings require a reference-assembly source and an implementation-image destination; reference markers do not authenticate either image.");
                Pin(mapping.Contract.Assembly); Pin(mapping.Implementation.Assembly);
                if (pairs.TryGetValue(mapping.Contract, out var prior) && prior != mapping.Implementation)
                    throw InvalidLibraryBinding("One contract definition maps to multiple implementation definitions.");
                pairs[mapping.Contract] = mapping.Implementation;
            }
            if (pairs.Count == 0) throw InvalidLibraryBinding("A library binding plan needs at least one explicit type mapping.");
            var immutable = pairs.ToImmutableDictionary();
            var provisional = new LibraryBindingPlan(this, immutable, []);
            var evidence = ImmutableArray.CreateBuilder<LibraryTypeBinding>();
            foreach (var pair in pairs.OrderBy(p => p.Key.Assembly.Identity.DisplayName, StringComparer.Ordinal)
                .ThenBy(p => p.Key.Assembly.ContentHash, StringComparer.Ordinal).ThenBy(p => MetadataTokens.GetToken(p.Key.Handle)))
            {
                var source = Header(pair.Key); var target = Header(pair.Value);
                if (source.Namespace != target.Namespace || !source.Names.SequenceEqual(target.Names)
                    || source.Flags != target.Flags || !source.Parameters.SequenceEqual(target.Parameters))
                    throw InvalidLibraryBinding("Mapped types must retain exact namespace/nesting/name, visibility, class/interface/abstract/sealed flags and generic parameter attributes.");
                var sourceParent = Parent(pair.Key); var targetParent = Parent(pair.Value);
                if (sourceParent is null ? targetParent is not null : targetParent != CanonicalLibraryType(provisional, sourceParent))
                    throw InvalidLibraryBinding("Nested type owners must have an explicit corresponding mapping.");
                if (Base(pair.Key) != Base(pair.Value))
                    throw InvalidLibraryBinding("Mapped base types must correspond structurally; include their required type mappings.");
                evidence.Add(new(Identity(pair.Key), Identity(pair.Value), source.Namespace, source.Names, source.Parameters.Length));
            }
            return ResolutionResult<LibraryBindingPlan>.Resolved(new(this, immutable, evidence.ToImmutable()));

            void Pin(ResolvedAssembly image)
            {
                if (images.TryGetValue(image.Identity, out string? hash))
                {
                    if (hash != image.ContentHash) throw InvalidLibraryBinding("Selected library images have conflicting content hashes for one assembly identity.");
                    return;
                }
                if (_candidates.ContainsKey(image.Identity))
                {
                    var resolved = ResolveAssembly(image.Identity);
                    if (!resolved.Success) throw new BindingFailureException(resolved.Diagnostics);
                    if (resolved.Value!.ContentHash != image.ContentHash)
                        throw InvalidLibraryBinding("Selected library image differs from its explicit catalogue snapshot.");
                }
                images.Add(image.Identity, image.ContentHash);
            }

            (string Namespace, ImmutableArray<string> Names, TypeAttributes Flags, ImmutableArray<GenericParameterAttributes> Parameters) Header(ResolvedType type)
            {
                var reader = type.Assembly.Image.Reader;
                var definition = reader.GetTypeDefinition(type.Handle);
                var parameters = ImmutableArray.CreateBuilder<GenericParameterAttributes>();
                foreach (var handle in definition.GetGenericParameters())
                {
                    InspectMember(budget);
                    var parameter = reader.GetGenericParameter(handle);
                    if (parameter.Index != parameters.Count || parameter.Parent != type.Handle)
                        throw InvalidLibraryBinding("Generic parameters must have contiguous indices and the expected owner.");
                    if (parameter.GetConstraints().Count != 0)
                        throw InvalidLibraryBinding("Nominal/dependent generic constraints require a later library binding profile.");
                    parameters.Add(parameter.Attributes);
                }
                var names = new List<string>();
                var seen = new HashSet<TypeDefinitionHandle>();
                string ns = "";
                for (var handle = type.Handle; !handle.IsNil;)
                {
                    InspectMember(budget);
                    if (!seen.Add(handle) || seen.Count > 64) throw InvalidLibraryBinding("Type nesting is cyclic or exceeds 64 levels.");
                    var current = reader.GetTypeDefinition(handle);
                    string name = reader.GetString(current.Name);
                    if (name.Length == 0 || name == "<Module>") throw InvalidLibraryBinding("Library mappings require named types, not module pseudo-types.");
                    names.Add(name); ns = reader.GetString(current.Namespace);
                    handle = current.GetDeclaringType();
                }
                names.Reverse();
                return (ns, [.. names], definition.Attributes & (TypeAttributes.VisibilityMask | TypeAttributes.ClassSemanticsMask
                    | TypeAttributes.Abstract | TypeAttributes.Sealed), parameters.ToImmutable());
            }

            ResolvedType? Parent(ResolvedType type)
            {
                var parent = type.Assembly.Image.Reader.GetTypeDefinition(type.Handle).GetDeclaringType();
                return parent.IsNil ? null : new(type.Assembly, parent);
            }

            TypeSignature? Base(ResolvedType type)
            {
                var handle = type.Assembly.Image.Reader.GetTypeDefinition(type.Handle).BaseType;
                return handle.IsNil ? null : ProjectLibrarySignature(provisional, new SignatureDecoder(this, type.Assembly).Type(handle), budget);
            }
        }
        catch (BindingFailureException exception) { return ResolutionResult<LibraryBindingPlan>.Failed(exception.Diagnostics); }
        catch (Exception exception) when (MalformedInput.IsMalformed(exception) || exception is UnsupportedBindingException)
        {
            return ResolutionResult<LibraryBindingPlan>.Failed(DiagnosticIds.LibraryBindingInvalid, "Cannot validate library binding metadata: " + exception.Message);
        }
    }

    /// <summary>Strictly resolves a TypeDef/TypeRef, then applies only this explicit plan.</summary>
    public ResolutionResult<ResolvedType> ResolveLibraryType(LibraryBindingPlan plan, ResolvedAssembly assembly, EntityHandle handle)
    {
        CheckLibraryPlan(plan); CheckOwner(assembly);
        try
        {
            CheckLibraryImage(plan, assembly);
            var resolved = ResolveType(assembly, handle);
            return resolved.Value is null ? resolved : ResolutionResult<ResolvedType>.Resolved(CanonicalLibraryType(plan, resolved.Value));
        }
        catch (BindingFailureException exception) { return ResolutionResult<ResolvedType>.Failed(exception.Diagnostics); }
    }

    /// <summary>Projects a decoded structural type through this plan without changing member binding.</summary>
    public ResolutionResult<TypeSignature> ResolveLibrarySignatureType(LibraryBindingPlan plan, ResolvedAssembly assembly,
        EntityHandle handle, MemberResolutionBudget? budget = null)
    {
        CheckLibraryPlan(plan);
        return Bind(assembly, handle, () =>
        {
            CheckLibraryImage(plan, assembly);
            return ProjectLibrarySignature(plan, new SignatureDecoder(this, assembly).Type(handle), budget ?? new());
        });
    }

    private void CheckLibraryPlan(LibraryBindingPlan plan)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(plan);
        if (!ReferenceEquals(plan.Owner, this)) throw new ArgumentException("The library binding plan belongs to a different resolver session.", nameof(plan));
    }

    private static LibraryTypeIdentity Identity(ResolvedType type) => new(type.Assembly.Identity, type.Assembly.ContentHash, MetadataTokens.GetToken(type.Handle));

    private static BindingFailureException InvalidLibraryBinding(string message) => Failure(DiagnosticIds.LibraryBindingInvalid, message);

    private static ResolvedType CanonicalLibraryType(LibraryBindingPlan plan, ResolvedType type)
    {
        CheckLibraryImage(plan, type.Assembly);
        if (!plan.ContractImages.ContainsKey(type.Assembly.Identity)) return type;
        if (plan.Mappings.TryGetValue(type, out var target)) return target;
        throw Failure(DiagnosticIds.LibraryTypeUnmapped,
            $"Contract type 0x{MetadataTokens.GetToken(type.Handle):x8} in '{type.Assembly.Identity.DisplayName}' has no library mapping. Add an implemented, reviewed type or remove the dependency.");
    }

    private static void CheckLibraryImage(LibraryBindingPlan plan, ResolvedAssembly assembly)
    {
        if (plan.SelectedImages.TryGetValue(assembly.Identity, out string? hash) && hash != assembly.ContentHash)
            throw InvalidLibraryBinding("Resolved image does not match the binding plan's pinned content hash.");
    }

    private static TypeSignature ProjectLibrarySignature(LibraryBindingPlan plan, TypeSignature type, MemberResolutionBudget budget)
    {
        InspectMember(budget);
        TypeSignature Project(TypeSignature value) => ProjectLibrarySignature(plan, value, budget);
        return type switch
        {
            TypeSignature.Named n => n with { Definition = CanonicalLibraryType(plan, n.Definition) },
            TypeSignature.Generic g => new TypeSignature.Generic((TypeSignature.Named)Project(g.Definition), new(g.Arguments.Select(Project))),
            TypeSignature.Vector v => new TypeSignature.Vector(Project(v.Element)),
            TypeSignature.Array a => a with { Element = Project(a.Element) },
            TypeSignature.ByReference b => new TypeSignature.ByReference(Project(b.Element)),
            TypeSignature.Pointer p => new TypeSignature.Pointer(Project(p.Element)),
            TypeSignature.Pinned p => new TypeSignature.Pinned(Project(p.Element)),
            TypeSignature.Modified m => new TypeSignature.Modified((TypeSignature.Named)Project(m.Modifier), m.Required, Project(m.Element)),
            TypeSignature.FunctionPointer f => new TypeSignature.FunctionPointer(f.Signature with
                { ReturnType = Project(f.Signature.ReturnType), Parameters = new(f.Signature.Parameters.Select(Project)) }),
            _ => type,
        };
    }
}
