using System.Collections.Immutable;
using System.Reflection;

namespace Cilica.Metadata.Resolution;

public sealed record TypeSource(string AssemblyIdentity, string ContentHash, int Token, string Name)
{
    public string Specialization { get; init; } = "";
}

/// <summary>A single TypeDef row's flags, without decoding fields or method bodies.</summary>
public sealed record TypeDefinitionFlags(TypeAttributes Attributes);

/// <summary>Structural input; named signatures remain owned by the resolver session.</summary>
public sealed record FieldInput(int Token, string Name, FieldAttributes Attributes,
    TypeSignature Signature, bool HasExplicitStorage);

/// <summary>Bounded type input for layout analysis, not permission to execute its methods.</summary>
public sealed record TypeInput(TypeSource Source, TypeAttributes Attributes, int OwnerGenericArity,
    bool IsReferenceAssembly, TypeSignature? BaseType, int InterfaceCount, int PackingSize, int ClassSize,
    bool HasTypeInitializer, bool HasModuleInitializer, ImmutableArray<FieldInput> Fields, int MetadataMembersRead)
{
    public ImmutableArray<TypeSignature> Interfaces { get; init; } = [];
    public bool ExternallyVisible { get; init; }
}

public sealed record TypeReadLimits(int MaximumFields = 4096, int MaximumMembers = 65536, int MaximumOwnerDepth = 64,
    int MaximumInterfaces = 256)
{
    // Bound the recursive display-name formatter even if a caller supplies larger limits.
    public const int MaximumSupportedOwnerDepth = 64;
}
