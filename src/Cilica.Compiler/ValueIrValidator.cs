using System;
using System.Collections.Generic;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Validates detached value copies using the existing ordered-value/call/control-flow verifier.</summary>
public static class ValueIrValidator
{
    public static void Validate(ValueIrProgram program) => _ = ValidateAndPlan(program);

    internal static ValueIrRules ValidateAndPlan(ValueIrProgram program, bool locations = false)
    {
        ArgumentNullException.ThrowIfNull(program);
        var rules = new ValueIrRules(program.Types, locations);
        IrValidator.Validate(new(program.Entry, program.Methods), program.Types.References, valueRules: rules);
        // Every inline layout must be used directly or via another used layout.
        var pending = new Stack<ObjectTypeId>(program.Methods.SelectMany(m => m.Parameters.Concat(m.Locals).Append(m.ReturnType)
            .Concat(m.Blocks.SelectMany(b => b.Inputs)).Concat(m.Blocks.SelectMany(b => b.Values).Select(v => v.Type)))
            .Select(t => t.Kind == IrTypeKind.ManagedLocation ? t.Element! : t)
            .Where(t => t.Kind == IrTypeKind.Value).Select(t => t.Class!));
        var visited = new HashSet<ObjectTypeId>();
        var layouts = program.Types.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
        while (pending.TryPop(out var id))
            if (visited.Add(id))
                foreach (var child in layouts[id].Fields.Select(f => f.Storage).OfType<ObjectStorage.Value>()) pending.Push(child.Type);
        if (visited.Count != layouts.Count)
            throw new ArgumentException("Invalid value program IR: unused inline layouts.", nameof(program));
        var references = program.Methods.SelectMany(m => m.Parameters.Concat(m.Locals).Append(m.ReturnType)
            .Concat(m.Blocks.SelectMany(b => b.Inputs)).Concat(m.Blocks.SelectMany(b => b.Values).Select(v => v.Type)))
            .Select(t => t.Kind == IrTypeKind.ManagedLocation ? t.Element! : t)
            .Where(t => t.Kind == IrTypeKind.Reference).Select(t => t.Class!)
            .Concat(layouts.Values.SelectMany(t => t.Fields).Select(f => f.Storage).OfType<ObjectStorage.Reference>().Select(r => r.Type));
        ObjectCatalogUsage.Validate(program.Types.References, references);
        return rules;
    }
}

internal sealed class ValueIrRules
{
    internal ValueTypeCatalog Catalog { get; }
    internal System.Collections.Immutable.ImmutableArray<ValueTypePlan> Plans { get; }
    internal ManagedLocationRules? Locations { get; }
    private readonly Dictionary<ObjectFieldId, ObjectField> _fields;
    internal ValueIrRules(ValueTypeCatalog catalog, bool locations = false)
    {
        Plans = ValueLayoutPlanner.Build(catalog);
        Catalog = catalog;
        _fields = catalog.Types.SelectMany(t => t.Fields).ToDictionary(f => f.Id);
        if (locations) Locations = new(catalog, this);
    }

    internal IrType? Field(IrType receiver, ObjectFieldId? id)
    {
        if (receiver.Kind != IrTypeKind.Value || id is null || receiver.Class != id.Owner || !_fields.TryGetValue(id, out var field)) return null;
        return field.Storage switch
        {
            ObjectStorage.Int32 => IrType.Int32,
            ObjectStorage.Reference reference => IrType.Reference(reference.Type),
            ObjectStorage.Value value => IrType.Value(value.Type),
            _ => null,
        };
    }
    internal bool ReadOnly(ObjectFieldId id) => (_fields[id].Attributes & System.Reflection.FieldAttributes.InitOnly) != 0;
}
