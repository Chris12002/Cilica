using System.Collections.Immutable;
using System.Reflection;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

/// <summary>A closed struct's declared inline fields; no object header or native offsets.</summary>
public sealed record ValueTypeLayout(TypeSource Source, TypeAttributes Attributes, ImmutableArray<ObjectField> Fields)
{
    public bool ExternallyVisible { get; init; } = (Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public;
    public ImmutableArray<ObjectTypeId> Interfaces { get; init; } = [];
}

/// <summary>Detached inline closure plus the existing, separately validated reference catalog.</summary>
public sealed record ValueTypeCatalog(ImmutableArray<ObjectTypeId> Roots, ObjectTypeId ValueBase,
    ImmutableArray<ValueTypeLayout> Types, ObjectTypeCatalog References)
{
    public const string Profile = "value-layout-v2";
}

/// <summary>One scalar copy slot. Each path preserves every declaring FieldDef, including nesting.</summary>
public sealed record ValueFieldPath(ImmutableArray<ObjectFieldId> Fields, ObjectStorage Storage);

/// <summary>Derived copy/tracing plan. An empty struct has no scalar slots but still has type identity.</summary>
public sealed record ValueTypePlan(ObjectTypeId Type, ImmutableArray<ValueFieldPath> Slots,
    int ExpandedFields, int Depth)
{
    public ImmutableArray<ValueFieldPath> References =>
        [.. System.Linq.Enumerable.Where(Slots, slot => slot.Storage is ObjectStorage.Reference)];
}

public static class ValueTypeLimits
{
    // Counts include repeated embeddings, even of structs with no scalar fields.
    public const int MaximumExpandedFields = 65536;
    public const int MaximumPathComponents = 262144;
    public const int MaximumDepth = 64;
}

public sealed record ValueCatalogResult(ValueTypeCatalog? Catalog, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Catalog is not null;
}
