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

    internal Dictionary<ObjectFieldId, ImmutableArray<ObjectStaticField>> Statics { get; } = [];

    internal HeapValueFields(ValueTypeCatalog values, IReadOnlyDictionary<ObjectTypeId, ValueTypePlan> plans)
    {
        var arrays = values.References.Arrays.Where(a => a.ArrayElement!.Kind == ObjectArrayElementKind.Value)
            .Select(a => a.ArrayElement!.Type!).Distinct().Select(type => new ObjectValueArrayLayout(type,
                plans[type].Slots.IsEmpty ? [new ObjectStorage.Int32()] : [.. plans[type].Slots.Select(s => s.Storage)]) { Empty = plans[type].Slots.IsEmpty }).ToImmutableArray();
        int count = 0;
        var statics = ImmutableArray.CreateBuilder<ObjectStaticField>();
        foreach (var field in values.References.Statics)
        {
            if (field.Id.Component != -1) throw new ArgumentException("Logical static fields cannot contain expanded components.");
            if (field.Type.Kind != IrTypeKind.Value) { statics.Add(field); ++count; continue; }
            var slots = plans[field.Type.Class!].Slots;
            int size = Math.Max(1, slots.Length);
            if (size > ObjectModelLimits.MaximumFields - count) throw new ArgumentException("Expanded static field limit exceeded.");
            count += size;
            var expanded = Enumerable.Range(0, size).Select(i => field with {
                Id = field.Id with { Component = i },
                Type = slots.IsEmpty ? IrType.Int32 : slots[i].Storage switch {
                    ObjectStorage.Int32 => IrType.Int32,
                    ObjectStorage.Int64 => IrType.Int64,
                    ObjectStorage.Reference reference => IrType.Reference(reference.Type),
                    _ => throw new ArgumentException("Static payload leaf is not scalar.")
                }
            }).ToImmutableArray();
            Statics.Add(field.Id, expanded); statics.AddRange(expanded);
        }
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
        var delegates = ImmutableArray.CreateBuilder<ObjectDelegateType>();
        int delegateParameters = 0;
        foreach (var declaration in values.References.Delegates)
        {
            var parameters = ImmutableArray.CreateBuilder<IrType>();
            foreach (var parameter in declaration.Parameters)
            {
                var expanded = ValueScalarLowering.ExpandSignatureType(parameter, plans);
                if (expanded.Length >= IrLimits.MaximumParameters - parameters.Count)
                    throw new ArgumentException("Expanded delegate parameter limit exceeded.");
                parameters.AddRange(expanded);
            }
            if (parameters.Count > IrLimits.MaximumParameters - delegateParameters)
                throw new ArgumentException("Aggregate expanded delegate signature limit exceeded.");
            delegateParameters += parameters.Count;
            delegates.Add(declaration with { Parameters = parameters.ToImmutable(), ParameterCount = parameters.Count });
        }
        Catalog = values.References with { Types = layouts.ToImmutable(), ValueArrays = arrays, Statics = statics.ToImmutable(), Delegates = delegates.ToImmutable() };
        ObjectModelValidator.Validate(Catalog);
    }
}
