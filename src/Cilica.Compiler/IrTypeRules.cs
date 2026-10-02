using System.Collections.Generic;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Class ancestry rules; the bounded, acyclic catalog is validated before use.</summary>
internal sealed class IrTypeRules
{
    private readonly Dictionary<ObjectTypeId, HashSet<ObjectTypeId>> _ancestors = [];

    internal IrTypeRules(ObjectTypeCatalog? catalog)
    {
        if (catalog is null) return;
        var layouts = catalog.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
        // Cache at most 256 * 256 identities; repeated IR assignments need no
        // ancestor traversal and cannot multiply hierarchy work by IR size.
        foreach (var id in layouts.Keys)
        {
            var ancestors = new HashSet<ObjectTypeId>();
            for (ObjectTypeId? current = id; current is not null; current = layouts[current].BaseType) ancestors.Add(current);
            _ancestors.Add(id, ancestors);
        }
    }

    internal bool Valid(IrType? type, bool storage) => type is not null && (type.Kind switch
    {
        IrTypeKind.Int32 => type.Class is null,
        IrTypeKind.Reference => type.Class?.ContentHash is { Length: 64 } && _ancestors.ContainsKey(type.Class),
        IrTypeKind.NullReference => _ancestors.Count != 0 && !storage && type.Class is null,
        _ => false,
    });

    internal bool Assignable(IrType? source, IrType? target) => source is not null && target is not null &&
        (source == target || target.Kind == IrTypeKind.Reference && (source == IrType.NullReference ||
            source.Kind == IrTypeKind.Reference && source.Class is { } id && target.Class is { } targetId
                && _ancestors.TryGetValue(id, out var ancestors) && ancestors.Contains(targetId)));

    internal bool Comparable(Int32Comparison comparison, IrType left, IrType right) =>
        left == IrType.Int32 && right == IrType.Int32 ||
        comparison is Int32Comparison.Equal or Int32Comparison.NotEqual &&
        left.Kind is IrTypeKind.Reference or IrTypeKind.NullReference &&
        right.Kind is IrTypeKind.Reference or IrTypeKind.NullReference;
}
