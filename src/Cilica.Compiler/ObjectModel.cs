using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

/// <summary>Nominal TypeDef identity, or a vector identity anchored to the selected Object plus its element.</summary>
public sealed record ObjectTypeId(string ContentHash, int Token, ObjectArrayElement? ArrayElement = null)
{
    public static ObjectTypeId From(TypeSource source) => new(source.ContentHash.ToLowerInvariant(), source.Token);
    public static ObjectTypeId Vector(ObjectTypeId objectBase, ObjectArrayElement element)
    {
        System.ArgumentNullException.ThrowIfNull(objectBase);
        System.ArgumentNullException.ThrowIfNull(element);
        if (objectBase.ArrayElement is not null) throw new System.ArgumentException("An array anchor must be a nominal Object identity.", nameof(objectBase));
        return new(objectBase.ContentHash, objectBase.Token, element);
    }
}

public enum ObjectArrayElementKind { Int32, Reference }
/// <summary>One-dimensional zero-based arrays of Int32 or a nominal class/interface reference; no nested vectors.</summary>
public sealed record ObjectArrayElement(ObjectArrayElementKind Kind, ObjectTypeId? Type = null)
{
    public static ObjectArrayElement Int32 { get; } = new(ObjectArrayElementKind.Int32);
    public static ObjectArrayElement Reference(ObjectTypeId type) => new(ObjectArrayElementKind.Reference, type);
}

/// <summary>A fieldless interface contract; it has no instance allocation layout.</summary>
public sealed record ObjectInterface(TypeSource Source, TypeAttributes Attributes, ImmutableArray<ObjectTypeId> Parents)
{
    // Top-level visibility follows the flags. Nested contracts require an explicit
    // assertion from the bounded owner walk; detached validation cannot reread metadata.
    public bool ExternallyVisible { get; init; } = (Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public;
}

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

/// <summary>Detached base/field/interface/array-element closure. This is a storage/type contract, not executable IR.</summary>
public sealed record ObjectModel(ObjectTypeId Entry, ObjectTypeId ObjectBase, ImmutableArray<ObjectTypeLayout> Types)
{
    public const string Profile = "object-layout-v5";
    public ImmutableArray<ObjectInterface> Interfaces { get; init; } = [];
    public ImmutableArray<ObjectTypeId> Arrays { get; init; } = [];
    // Selected runtime identity, represented intrinsically rather than by mutable fields.
    public ObjectTypeId? String { get; init; }
}

/// <summary>Explicit nominal/vector roots and their shared closure, including the selected object base.</summary>
public sealed record ObjectTypeCatalog(ImmutableArray<ObjectTypeId> Roots, ObjectTypeId ObjectBase,
    ImmutableArray<ObjectTypeLayout> Types)
{
    public ImmutableArray<ObjectInterface> Interfaces { get; init; } = [];
    public ImmutableArray<ObjectTypeId> Arrays { get; init; } = [];
    // Selected runtime identity, represented intrinsically rather than by mutable fields.
    public ObjectTypeId? String { get; init; }
}

public static class ObjectStringLimits
{
    public const int MaximumLiteralCodeUnits = 65536;
    public const int MaximumTotalLiteralCodeUnits = 262144;
    public const int MaximumDistinctLiterals = 256;
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
