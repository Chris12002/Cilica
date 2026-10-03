using System.Collections.Generic;

namespace Cilica.Compiler;

/// <summary>Class/interface ancestry rules; the bounded, acyclic catalog is validated before use.</summary>
internal sealed class IrTypeRules
{
    private readonly Dictionary<ObjectTypeId, HashSet<ObjectTypeId>> _ancestors = [];
    private readonly ObjectTypeId? _object;

    internal IrTypeRules(ObjectTypeCatalog? catalog)
    {
        if (catalog is null) return;
        _object = catalog.ObjectBase;
        _ancestors = new ObjectTypeRelations(catalog).Ancestors;
    }

    internal bool Valid(IrType? type, bool storage) => type is not null && (type.Kind switch
    {
        IrTypeKind.Int32 => type.Class is null,
        IrTypeKind.Reference => _object is not null && ObjectModelValidator.ReferenceIdentity(type.Class, _object)
            && _ancestors.ContainsKey(type.Class!),
        IrTypeKind.NullReference => _ancestors.Count != 0 && !storage && type.Class is null,
        _ => false,
    });

    // Calls may inspect a callee before its body has been validated. Check both
    // identities before equality/hash lookup, including forged structural types.
    internal bool Assignable(IrType? source, IrType? target) => source is not null && target is not null
        && Valid(source, false) && Valid(target, false) &&
        (source == target || target.Kind == IrTypeKind.Reference && (source == IrType.NullReference ||
            source.Kind == IrTypeKind.Reference && source.Class is { } id && target.Class is { } targetId
                && _ancestors.TryGetValue(id, out var ancestors) && ancestors.Contains(targetId)));

    internal bool Comparable(Int32Comparison comparison, IrType left, IrType right) =>
        left == IrType.Int32 && right == IrType.Int32 ||
        comparison is Int32Comparison.Equal or Int32Comparison.NotEqual &&
        left.Kind is IrTypeKind.Reference or IrTypeKind.NullReference &&
        right.Kind is IrTypeKind.Reference or IrTypeKind.NullReference;
}
