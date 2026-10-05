using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Cilica.Compiler;

// Logical FieldDef identities survive expansion. Component numbers denote the
// validated value plan's ordered leaves, never additional metadata declarations.
internal sealed class HeapValueFields
{
    internal ObjectTypeCatalog Catalog { get; }
    internal Dictionary<ObjectFieldId, ImmutableArray<ObjectField>> Fields { get; } = [];

    internal HeapValueFields(ValueTypeCatalog values, IReadOnlyDictionary<ObjectTypeId, ValueTypePlan> plans)
    {
        var arrays = values.References.Arrays.Where(a => a.ArrayElement!.Kind == ObjectArrayElementKind.Value)
            .Select(a => a.ArrayElement!.Type!).Distinct().Select(type => new ObjectValueArrayLayout(type,
                plans[type].Slots.IsEmpty ? [new ObjectStorage.Int32()] : [.. plans[type].Slots.Select(s => s.Storage)])).ToImmutableArray();
        if (!values.References.Types.Any(t => t.Fields.Any(f => f.Storage is ObjectStorage.Value)))
        { Catalog = values.References with { ValueArrays = arrays }; ObjectModelValidator.Validate(Catalog); return; }
        int count = 0;
        var layouts = ImmutableArray.CreateBuilder<ObjectTypeLayout>();
        foreach (var layout in values.References.Types)
        {
            var fields = ImmutableArray.CreateBuilder<ObjectField>();
            foreach (var field in layout.Fields)
            {
                if (field.Id.Component != -1) throw new ArgumentException("Logical heap fields cannot contain expanded components.");
                if (field.Storage is not ObjectStorage.Value value) { fields.Add(field); ++count; continue; }
                var slots = plans[value.Type].Slots;
                int size = Math.Max(1, slots.Length);
                if (size > ObjectModelLimits.MaximumFields - count)
                    throw new ArgumentException("Expanded heap field limit exceeded.");
                count += size;
                // An empty value still needs a checked receiver for load/store/
                // address operations. This private zero slot is never observable.
                var expanded = Enumerable.Range(0, size).Select(i => field with {
                    Id = field.Id with { Component = i },
                    Storage = slots.IsEmpty ? new ObjectStorage.Int32() : slots[i].Storage
                }).ToImmutableArray();
                Fields.Add(field.Id, expanded); fields.AddRange(expanded);
            }
            if (count > ObjectModelLimits.MaximumFields) throw new ArgumentException("Expanded heap field limit exceeded.");
            layouts.Add(layout with { Fields = fields.ToImmutable() });
        }
        Catalog = values.References with { Types = layouts.ToImmutable(), ValueArrays = arrays };
        ObjectModelValidator.Validate(Catalog);
    }
}
