using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace Cilica.Compiler;

/// <summary>Validates inline containment and derives bounded scalar copy/reference paths without recursion.</summary>
public static class ValueLayoutPlanner
{
    public static ImmutableArray<ValueTypePlan> Build(ValueTypeCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        void Require([DoesNotReturnIf(false)] bool condition, string message)
        {
            if (!condition) throw new ArgumentException("Invalid value layout model: " + message, nameof(catalog));
        }
        Require(catalog.References is not null, "reference catalog is missing.");
        ObjectModelValidator.Validate(catalog.References);
        var references = catalog.References.Types.Select(t => ObjectTypeId.From(t.Source))
            .Concat(catalog.References.Interfaces.Select(t => ObjectTypeId.From(t.Source)))
            .Concat(catalog.References.Arrays).Concat(catalog.References.Delegates.Select(d => ObjectTypeId.From(d.Source))).ToHashSet();
        if (catalog.References.String is { } text) references.Add(text);
        Require(ObjectModelValidator.Identity(catalog.ValueBase, 0x02) && !references.Contains(catalog.ValueBase),
            "selected ValueType needs a distinct canonical nominal identity.");
        Require(!catalog.Types.IsDefault && catalog.Types.Length <= ObjectModelLimits.MaximumTypes
            && catalog.Types.Length + references.Count + 1 <= ObjectModelLimits.MaximumTypes,
            "combined type limit exceeded or value types missing.");
        Require(!catalog.Roots.IsDefault && catalog.Roots.Length <= ObjectModelLimits.MaximumTypes
            && catalog.Roots.All(id => ObjectModelValidator.Identity(id, 0x02))
            && catalog.Roots.Distinct().Count() == catalog.Roots.Length, "invalid or duplicate roots.");
        var types = new Dictionary<ObjectTypeId, ValueTypeLayout>();
        var fieldIds = catalog.References.Types.SelectMany(t => t.Fields)
            .Select(f => (f.Id.Owner.ContentHash, f.Id.Owner.Specialization, f.Id.Token)).ToHashSet();
        int fields = fieldIds.Count;
        foreach (var type in catalog.Types)
        {
            Require(type?.Source is not null && type.Source.Name is not null && type.Source.AssemblyIdentity is not null
                && type.Source.ContentHash is not null, "missing type source.");
            var id = ObjectTypeId.From(type.Source);
            Require(ObjectModelValidator.Identity(id, 0x02) && id != catalog.ValueBase
                && !references.Contains(id) && types.TryAdd(id, type), "invalid, duplicate or reference/value identity collision.");
            Require(AttributesSupported(type.Attributes), "struct attributes require sealed automatic/sequential managed layout.");
            Require(!type.Fields.IsDefault && type.Fields.Length <= ObjectModelLimits.MaximumFields - fields,
                "combined field limit exceeded or fields missing.");
            fields += type.Fields.Length;
            int previous = 0;
            foreach (var field in type.Fields)
            {
                Require(field?.Id is not null && field.Id.Owner == id && (uint)field.Id.Token >> 24 == 0x04
                    && (field.Id.Token & 0xffffff) != 0 && field.Id.Token > previous
                    && fieldIds.Add((id.ContentHash, id.Specialization, field.Id.Token)), "fields need ordered unique image-scoped FieldDef identities and their declaring owner.");
                previous = field.Id.Token;
                Require(field.Name is not null && ObjectModelValidator.FieldAttributesSupported(field.Attributes),
                    "unsupported field name/attributes.");
                Require(field.Storage is ObjectStorage.Int32
                    || field.Storage is ObjectStorage.Reference reference && references.Contains(reference.Type)
                    || field.Storage is ObjectStorage.Value value && ObjectModelValidator.Identity(value.Type, 0x02),
                    "fields require Int32, included object references or canonical inline values.");
            }
        }
        Require(catalog.Roots.All(types.ContainsKey), "root is outside the value catalog.");
        var children = new Dictionary<ObjectTypeId, HashSet<ObjectTypeId>>();
        var parents = types.Keys.ToDictionary(id => id, _ => new List<ObjectTypeId>());
        foreach (var (id, type) in types)
        {
            var nested = type.Fields.Select(f => f.Storage).OfType<ObjectStorage.Value>().Select(v => v.Type).ToHashSet();
            Require(nested.All(types.ContainsKey), "inline field target is outside the value catalog.");
            children.Add(id, nested);
            foreach (var child in nested) parents[child].Add(id);
        }
        var included = new HashSet<ObjectTypeId>();
        var pending = new Stack<ObjectTypeId>(catalog.Roots);
        while (pending.TryPop(out var id))
            if (included.Add(id)) foreach (var child in children[id]) pending.Push(child);
        Require(included.Count == types.Count, "catalog contains unrelated inline types.");

        // A topological walk handles deep graphs without using the compiler stack.
        // Duplicate embeddings count once for dependency order, but each consumes
        // layout space/work when paths are copied below.
        var outstanding = children.ToDictionary(pair => pair.Key, pair => pair.Value.Count);
        var ready = new Queue<ObjectTypeId>(types.Keys.Where(id => outstanding[id] == 0));
        var plans = new Dictionary<ObjectTypeId, ValueTypePlan>();
        int expandedTotal = 0, pathTotal = 0;
        while (ready.TryDequeue(out var id))
        {
            int expanded = 0, depth = 0;
            var slots = ImmutableArray.CreateBuilder<ValueFieldPath>();
            foreach (var field in types[id].Fields)
            {
                var child = field.Storage is ObjectStorage.Value nested ? plans[nested.Type] : null;
                int cost = 1 + (child?.ExpandedFields ?? 0);
                Require(cost <= ValueTypeLimits.MaximumExpandedFields - expandedTotal,
                    "expanded field limit exceeded.");
                expandedTotal += cost; expanded += cost;
                depth = Math.Max(depth, 1 + (child?.Depth ?? 0));
                Require(depth <= ValueTypeLimits.MaximumDepth, "inline nesting depth limit exceeded.");
                if (child is null)
                {
                    Require(pathTotal < ValueTypeLimits.MaximumPathComponents, "field-path component limit exceeded.");
                    ++pathTotal;
                    slots.Add(new([field.Id], field.Storage));
                }
                else foreach (var slot in child.Slots)
                {
                    int count = slot.Fields.Length + 1;
                    Require(count <= ValueTypeLimits.MaximumPathComponents - pathTotal, "field-path component limit exceeded.");
                    pathTotal += count;
                    slots.Add(new([field.Id, .. slot.Fields], slot.Storage));
                }
            }
            plans.Add(id, new(id, slots.ToImmutable(), expanded, depth));
            foreach (var parent in parents[id]) if (--outstanding[parent] == 0) ready.Enqueue(parent);
        }
        Require(plans.Count == types.Count, "recursive inline containment has no finite layout.");
        return [.. catalog.Types.Select(t => plans[ObjectTypeId.From(t.Source)])];
    }

    internal static bool AttributesSupported(TypeAttributes attributes) =>
        (attributes & TypeAttributes.Sealed) != 0
        && (attributes & TypeAttributes.LayoutMask) is TypeAttributes.AutoLayout or TypeAttributes.SequentialLayout
        && ObjectModelValidator.ClassAttributes(attributes & ~TypeAttributes.LayoutMask);
}
