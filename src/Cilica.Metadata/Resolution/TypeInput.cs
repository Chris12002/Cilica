using System.Collections.Immutable;
using System.Reflection;

namespace Cilica.Metadata.Resolution;

public sealed record TypeSource(string AssemblyIdentity, string ContentHash, int Token, string Name)
{
    public string Specialization { get; init; } = "";
    public string? RuntimeName { get; init; }
}

/// <summary>A single TypeDef row's flags, without decoding fields or method bodies.</summary>
public sealed record TypeDefinitionFlags(TypeAttributes Attributes);

/// <summary>Direct base signature only; not a layout, delegate shape or execution grant.</summary>
public sealed record TypeBaseInput(TypeSignature? BaseType);

/// <summary>Declaration nesting and visibility only; not a structural layout or execution grant.</summary>
public sealed record TypeAccessInput(TypeSource Source, int OwnerGenericArity,
    bool ExternallyVisible, ImmutableArray<LexicalTypeOwner> LexicalOwners);

public sealed record LexicalTypeOwner(int Token, TypeAttributes Visibility, int GenericArity);

/// <summary>Structural input; named signatures remain owned by the resolver session.</summary>
public sealed record FieldInput(int Token, string Name, FieldAttributes Attributes,
    TypeSignature Signature, bool HasExplicitStorage)
{
    // Exact primitive literal metadata, without RVA, offset or marshal storage.
    // This grants no permission to load/address/write the field through IL.
    public bool IsMetadataConstant { get; init; }
}

/// <summary>Bounded type input for layout analysis, not permission to execute its methods.</summary>
public sealed record TypeInput(TypeSource Source, TypeAttributes Attributes, int OwnerGenericArity,
    bool IsReferenceAssembly, TypeSignature? BaseType, int InterfaceCount, int PackingSize, int ClassSize,
    bool HasTypeInitializer, bool HasModuleInitializer, ImmutableArray<FieldInput> Fields, int MetadataMembersRead)
{
    public ImmutableArray<LexicalTypeOwner> LexicalOwners { get; init; } = [];
    public ImmutableArray<TypeSignature> Interfaces { get; init; } = [];
    public bool ExternallyVisible { get; init; }
    public bool IsByRefLike { get; init; }
    // Populated by the closed snapshot reader in declaration-index order.
    public ImmutableArray<GenericParameterAttributes> ClosedParameterAttributes { get; init; } = [];
}

public sealed record TypeReadLimits(int MaximumFields = 4096, int MaximumMembers = 65536, int MaximumOwnerDepth = 64,
    int MaximumInterfaces = 256)
{
    // Bound the recursive display-name formatter even if a caller supplies larger limits.
    public const int MaximumSupportedOwnerDepth = 64;
}
