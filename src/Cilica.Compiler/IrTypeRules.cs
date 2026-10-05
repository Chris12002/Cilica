using System.Collections.Generic;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Class/interface ancestry rules; the bounded, acyclic catalog is validated before use.</summary>
internal sealed class IrTypeRules
{
    private readonly Dictionary<ObjectTypeId, HashSet<ObjectTypeId>> _ancestors = [];
    private readonly ObjectTypeId? _object;
    private readonly HashSet<ObjectTypeId> _values = [];
    private readonly bool _locations;
    private readonly bool _integers64;
    internal bool IntegerCasts => _integers64;

    internal IrTypeRules(ObjectTypeCatalog? catalog, ValueTypeCatalog? values = null, bool locations = false, bool integers64 = false)
    {
        _locations = locations;
        _integers64 = integers64;
        if (catalog is null) return;
        _object = catalog.ObjectBase;
        _ancestors = new ObjectTypeRelations(catalog).Ancestors;
        if (values is not null) foreach (var type in values.Types) _values.Add(ObjectTypeId.From(type.Source));
    }

    internal bool Valid(IrType? type, bool storage) => type is not null && (type.Kind == IrTypeKind.ManagedLocation
        ? _locations && type.Class is null && type.Element is { Element: null, ReadOnly: false } element
            && element.Kind is IrTypeKind.Int32 or IrTypeKind.Reference or IrTypeKind.Value && Valid(element, true)
        : type.Element is null && !type.ReadOnly && (type.Kind switch
    {
        IrTypeKind.Int32 => type.Class is null,
        IrTypeKind.Int64 => _integers64 && type.Class is null,
        IrTypeKind.Reference => _object is not null && ObjectModelValidator.ReferenceIdentity(type.Class, _object)
            && _ancestors.ContainsKey(type.Class!),
        IrTypeKind.NullReference => _ancestors.Count != 0 && !storage && type.Class is null,
        IrTypeKind.Value => ObjectModelValidator.Identity(type.Class, 0x02) && _values.Contains(type.Class!),
        _ => false,
    }));

    // Calls may inspect a callee before its body has been validated. Check both
    // identities before equality/hash lookup, including forged structural types.
    internal bool Assignable(IrType? source, IrType? target) => source is not null && target is not null
        && Valid(source, false) && Valid(target, false) &&
        (source == target || source.Kind == IrTypeKind.ManagedLocation && target.Kind == IrTypeKind.ManagedLocation
            && source.Element == target.Element && (!source.ReadOnly || target.ReadOnly)
        || target.Kind == IrTypeKind.Reference && (source == IrType.NullReference ||
            source.Kind == IrTypeKind.Reference && source.Class is { } id && target.Class is { } targetId
                && _ancestors.TryGetValue(id, out var ancestors) && ancestors.Contains(targetId)));

    internal IrType? Join(IrType left, IrType right)
    {
        if (Assignable(left, right)) return right;
        if (Assignable(right, left)) return left;
        if (left.Kind != IrTypeKind.Reference || right.Kind != IrTypeKind.Reference) return null;
        var common = _ancestors[left.Class!].Intersect(_ancestors[right.Class!]).ToArray();
        var minimum = common.Where(t => _ancestors[t].Count == common.Length).ToArray();
        return minimum.Length == 1 ? IrType.Reference(minimum[0]) : null;
    }

    internal bool Comparable(Int32Comparison comparison, IrType left, IrType right) =>
        left == IrType.Int32 && right == IrType.Int32 ||
        _integers64 && left == IrType.Int64 && right == IrType.Int64 ||
        comparison is Int32Comparison.Equal or Int32Comparison.NotEqual &&
        left.Kind is IrTypeKind.Reference or IrTypeKind.NullReference &&
        right.Kind is IrTypeKind.Reference or IrTypeKind.NullReference;

    internal static bool TruthTest(IrType type) => type.Kind is IrTypeKind.Int32 or IrTypeKind.Int64 or IrTypeKind.Reference or IrTypeKind.NullReference;
}
