using System.Collections.Generic;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Rules for the direct-System.Object class profile; the catalog is validated before use.</summary>
internal sealed class IrTypeRules(ObjectTypeCatalog? catalog)
{
    private readonly HashSet<ObjectTypeId> _classes = catalog is null ? [] :
        [.. catalog.Types.Select(t => ObjectTypeId.From(t.Source))];

    internal bool Valid(IrType? type, bool storage) => type is not null && (type.Kind switch
    {
        IrTypeKind.Int32 => type.Class is null,
        IrTypeKind.Reference => catalog is not null && type.Class?.ContentHash is { Length: 64 } && _classes.Contains(type.Class),
        IrTypeKind.NullReference => catalog is not null && !storage && type.Class is null,
        _ => false,
    });

    internal bool Assignable(IrType? source, IrType? target) => source is not null && target is not null &&
        (source == target || target.Kind == IrTypeKind.Reference && (source == IrType.NullReference ||
            source.Kind == IrTypeKind.Reference && target.Class == catalog?.ObjectBase));

    internal bool Comparable(Int32Comparison comparison, IrType left, IrType right) =>
        left == IrType.Int32 && right == IrType.Int32 ||
        comparison is Int32Comparison.Equal or Int32Comparison.NotEqual &&
        left.Kind is IrTypeKind.Reference or IrTypeKind.NullReference &&
        right.Kind is IrTypeKind.Reference or IrTypeKind.NullReference;
}
