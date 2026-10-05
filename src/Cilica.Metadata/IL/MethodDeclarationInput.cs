using System.Collections.Immutable;
using System.Reflection;
using Cilica.Metadata.Resolution;

namespace Cilica.Metadata.IL;

/// <summary>Bounded declaration facts without reading IL; signatures remain resolver-owned.</summary>
public sealed record MethodDeclarationInput(MethodSource Source, MethodSignature Signature,
    MethodAttributes Attributes, MethodImplAttributes Implementation, int OwnerGenericArity,
    bool IsReferenceAssembly, bool HasTypeInitializer, bool HasModuleInitializer,
    int OwnerToken, TypeAttributes OwnerAttributes, bool OwnerExternallyVisible, bool HasBody,
    string DeclaredName, int MetadataMembersRead)
{
    public ImmutableArray<LexicalTypeOwner> LexicalOwners { get; init; } = [];
    public bool OwnerAssemblyVisible { get; init; }
}
