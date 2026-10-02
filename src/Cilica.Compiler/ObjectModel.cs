using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

/// <summary>Content-scoped TypeDef identity, independent of names, paths and readers.</summary>
public sealed record ObjectTypeId(string ContentHash, int Token)
{
    public static ObjectTypeId From(TypeSource source) => new(source.ContentHash.ToLowerInvariant(), source.Token);
}

/// <summary>A fieldless interface contract; it has no instance allocation layout.</summary>
public sealed record ObjectInterface(TypeSource Source, TypeAttributes Attributes, ImmutableArray<ObjectTypeId> Parents);

/// <summary>Field identity retains its declaring type, including its input image.</summary>
public sealed record ObjectFieldId(ObjectTypeId Owner, int Token);

/// <summary>Logical storage kinds; a reference is never an Int32 or an untracked native pointer.</summary>
public abstract record ObjectStorage
{
    public sealed record Int32 : ObjectStorage;
    public sealed record Reference(ObjectTypeId Type) : ObjectStorage;
}

public sealed record ObjectField(ObjectFieldId Id, string Name, FieldAttributes Attributes, ObjectStorage Storage);

/// <summary>Ordered declared fields, without inherited copies; native byte layouts are a separate lowering.</summary>
public sealed record ObjectTypeLayout(TypeSource Source, ObjectTypeId? BaseType,
    TypeAttributes Attributes, ImmutableArray<ObjectField> Fields)
{
    public ImmutableArray<ObjectTypeId> Interfaces { get; init; } = [];
    // Derived from field kinds so a caller cannot supply a contradictory tracing map.
    public ImmutableArray<int> ReferenceSlots => Fields.IsDefault ? default :
        [.. Fields.Select((descriptor, slot) => (descriptor, slot)).Where(p => p.descriptor.Storage is ObjectStorage.Reference).Select(p => p.slot)];
}

/// <summary>Detached base-class, field and interface closure. This is a storage/type contract, not executable IR.</summary>
public sealed record ObjectModel(ObjectTypeId Entry, ObjectTypeId ObjectBase, ImmutableArray<ObjectTypeLayout> Types)
{
    public const string Profile = "object-layout-v3";
    public ImmutableArray<ObjectInterface> Interfaces { get; init; } = [];
}

/// <summary>Explicit class/interface roots and their shared closure, including the selected object base.</summary>
public sealed record ObjectTypeCatalog(ImmutableArray<ObjectTypeId> Roots, ObjectTypeId ObjectBase,
    ImmutableArray<ObjectTypeLayout> Types)
{
    public ImmutableArray<ObjectInterface> Interfaces { get; init; } = [];
}

public static class ObjectModelLimits
{
    public const int MaximumTypes = 256;
    public const int MaximumFields = 4096;
    public const int MaximumMetadataMembers = 65536;
    public const int MaximumOwnerDepth = TypeReadLimits.MaximumSupportedOwnerDepth;
}

public sealed record ObjectModelResult(ObjectModel? Model, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Model is not null;
}

public sealed record ObjectCatalogResult(ObjectTypeCatalog? Catalog, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Catalog is not null;
}
