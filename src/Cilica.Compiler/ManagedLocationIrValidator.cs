using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Cilica.Compiler;

/// <summary>Checks synchronous, nonescaping storage borrows. No raw/native address is part of this contract.</summary>
public static class ManagedLocationIrValidator
{
    public static void Validate(ManagedLocationIrProgram program) => _ = ValidateAndPlan(program);

    internal static ValueIrRules ValidateAndPlan(ManagedLocationIrProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(program.Flow);
        var rules = ValueIrValidator.ValidateAndPlan(program.Flow, locations: true);
        var entry = program.Flow.Methods.Single(m => IrMethodId.From(m.Source) == program.Flow.Entry);
        if (entry.Parameters.Any(p => p.Kind == IrTypeKind.ManagedLocation))
            throw new ArgumentException("Managed location entry cannot borrow storage without a caller lifetime.", nameof(program));
        return rules;
    }
}

internal sealed class ManagedLocationRules
{
    private readonly Dictionary<ObjectFieldId, ObjectField> _heapFields;
    private readonly IrTypeRules _types;
    private readonly ValueIrRules? _values;

    internal ManagedLocationRules(ValueTypeCatalog catalog, ValueIrRules values) : this(catalog.References, catalog, values) { }
    internal ManagedLocationRules(ObjectTypeCatalog references, ValueTypeCatalog? catalog = null, ValueIrRules? values = null)
    {
        _heapFields = references.Types.SelectMany(t => t.Fields).ToDictionary(f => f.Id);
        _types = new(references, catalog, locations: true); _values = values;
    }

    internal static bool Address(IrType storage, IrType result, bool readOnly = false) =>
        storage.Kind is IrTypeKind.Int32 or IrTypeKind.Reference or IrTypeKind.Value
        && result.Kind == IrTypeKind.ManagedLocation && result.Element == storage && (!readOnly || result.ReadOnly);

    internal bool HeapField(IrType receiver, ObjectFieldId? id, IrType result) => id is not null
        && _heapFields.TryGetValue(id, out var field) && _types.Assignable(receiver, IrType.Reference(id.Owner))
        && Storage(field.Storage) is { } element && Address(element, result, (field.Attributes & FieldAttributes.InitOnly) != 0);

    internal bool NestedField(IrType parent, ObjectFieldId? id, IrType result) =>
        parent.Kind == IrTypeKind.ManagedLocation && parent.Element is { } element
        && _values?.Field(element, id) is { } fieldType && Address(fieldType, result, parent.ReadOnly || _values.ReadOnly(id!));

    internal static bool Load(IrType location, IrType result) => location.Kind == IrTypeKind.ManagedLocation && location.Element == result;
    internal bool Store(IrType location, IrType value) => Writable(location) && _types.Assignable(value, location.Element);
    internal static bool Writable(IrType location) => location.Kind == IrTypeKind.ManagedLocation && !location.ReadOnly;

    private static IrType? Storage(ObjectStorage storage) => storage switch
    {
        ObjectStorage.Int32 => IrType.Int32,
        ObjectStorage.Reference reference => IrType.Reference(reference.Type),
        ObjectStorage.Value value => IrType.Value(value.Type),
        _ => null,
    };
}
