using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Cilica.Compiler;

public sealed record EmbeddingRecordField(uint Id, string Name, ImmutableArray<ObjectFieldId> Path, EmbeddingValueKind Kind);
public sealed record EmbeddingRecordBinding(uint Id, string Name, ObjectTypeId Type, ImmutableArray<EmbeddingRecordField> Fields);

/// <summary>Maps stable host field order to the compiler's validated scalar slot order.</summary>
internal sealed class EmbeddingRecordPlan
{
    internal sealed record Field(EmbeddingRecordField Binding, int NativeSlot);
    internal sealed record Record(EmbeddingRecordBinding Binding, ValueTypePlan Layout, ImmutableArray<Field> Fields);
    internal sealed record Input(EmbeddingValueShape Shape, int NativeSlot);
    internal ImmutableDictionary<uint, Record> Records { get; }
    internal static EmbeddingRecordPlan Empty { get; } = new(ImmutableDictionary<uint, Record>.Empty);
    private EmbeddingRecordPlan(ImmutableDictionary<uint, Record> records) => Records = records;

    internal static EmbeddingRecordPlan Build(ValueTypeCatalog catalog, ImmutableArray<EmbeddingRecordBinding> bindings)
    {
        void Require(bool condition, string message) { if (!condition) throw new ArgumentException("Invalid embedding record contract: " + message, nameof(bindings)); }
        Require(!bindings.IsDefault && bindings.Length <= 32, "at most 32 record declarations required.");
        if (bindings.IsEmpty) return Empty;
        Require(bindings.All(b => b is not null && b.Id != 0 && EmbeddingModuleEmitter.ValidName(b.Name) && b.Type is not null), "record declaration.");
        Require(bindings.Select(b => b.Id).Distinct().Count() == bindings.Length
            && bindings.Select(b => b.Name).Distinct(StringComparer.Ordinal).Count() == bindings.Length
            && bindings.Select(b => b.Type).Distinct().Count() == bindings.Length, "duplicate record ID/name/type.");
        var layouts = ValueLayoutPlanner.Build(catalog).ToDictionary(p => p.Type);
        var declarations = catalog.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
        var result = ImmutableDictionary.CreateBuilder<uint, Record>();
        foreach (var binding in bindings) {
            Require(layouts.ContainsKey(binding.Type), "record type is outside this value closure.");
            var layout = layouts[binding.Type];
            Require(declarations[binding.Type].ExternallyVisible, "record type must be externally visible.");
            Require(layout.Slots.Length is > 0 and <= 64 && !binding.Fields.IsDefault && binding.Fields.Length == layout.Slots.Length,
                "record must declare all 1..64 scalar fields; empty records are not admitted.");
            Require(binding.Fields.All(f => f is not null && f.Id != 0 && EmbeddingModuleEmitter.ValidName(f.Name)
                && !f.Path.IsDefaultOrEmpty && f.Kind is EmbeddingValueKind.Int32 or EmbeddingValueKind.Integer64), "numeric field declaration.");
            Require(binding.Fields.Select(f => f.Id).Distinct().Count() == binding.Fields.Length
                && binding.Fields.Select(f => f.Name).Distinct(StringComparer.Ordinal).Count() == binding.Fields.Length, "duplicate field ID/name.");
            var fields = ImmutableArray.CreateBuilder<Field>();
            foreach (var field in binding.Fields.OrderBy(f => f.Id)) {
                int index = Array.FindIndex(layout.Slots.ToArray(), slot => slot.Fields.SequenceEqual(field.Path));
                Require(index >= 0, "field path must name an exact scalar leaf.");
                Require(field.Kind == Kind(layout.Slots[index].Storage), "field width mismatch or reference-bearing record.");
                foreach (var part in field.Path) {
                    // Every nested declaring value and field is public, not just the outer type.
                    var owner = declarations.Values.Single(t => t.Fields.Any(f => f.Id == part));
                    var declared = owner.Fields.Single(f => f.Id == part);
                    Require(owner.ExternallyVisible && (declared.Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public,
                        "record paths cannot expose private fields/types.");
                }
                fields.Add(new(field, index));
            }
            Require(fields.Select(f => f.NativeSlot).Distinct().Count() == layout.Slots.Length, "duplicate or missing field path.");
            result.Add(binding.Id, new(binding, layout, fields.ToImmutable()));
        }
        return new(result.ToImmutable());
    }

    internal static EmbeddingValueKind Kind(ObjectStorage storage) => storage switch {
        ObjectStorage.Int32 => EmbeddingValueKind.Int32,
        ObjectStorage.Int64 => EmbeddingValueKind.Integer64,
        _ => throw new ArgumentException("Embedding records currently admit only Int32/Integer64 scalar leaves.")
    };
    internal static IrType Scalar(ObjectStorage storage) => Kind(storage) == EmbeddingValueKind.Int32 ? IrType.Int32 : IrType.Int64;

    internal ImmutableArray<Input> Inputs(ImmutableArray<EmbeddingValueShape> parameters)
    {
        var result = ImmutableArray.CreateBuilder<Input>(); int start = 0;
        foreach (var shape in parameters) {
            if (shape.Kind == EmbeddingValueKind.Record) {
                var record = Records[shape.RecordTypeId];
                result.AddRange(record.Fields.Select(f => new Input(new(f.Binding.Kind), start + f.NativeSlot)));
                start += record.Fields.Length;
            } else { result.Add(new(shape, start)); ++start; }
        }
        return result.ToImmutable();
    }
}
