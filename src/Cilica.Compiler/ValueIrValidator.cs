using System;
using System.Collections.Generic;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Validates detached value copies using the existing ordered-value/call/control-flow verifier.</summary>
public static class ValueIrValidator
{
    public static void Validate(ValueIrProgram program) => _ = ValidateAndPlan(program);

    internal static ValueIrRules ValidateAndPlan(ValueIrProgram program, bool locations = false, ObjectIrRules? objects = null, bool boxing = false, ManagedExceptionRules? exceptions = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        var rules = new ValueIrRules(program.Types, locations, boxing);
        if (!boxing && program.Types.Types.Any(t => !t.Interfaces.IsEmpty))
            throw new ArgumentException("Value interface declarations require a composed scalar profile that retains the value metadata catalog.", nameof(program));
        IrValidator.Validate(new(program.Entry, program.Methods), program.Types.References, objectRules: objects, valueRules: rules, exceptions: exceptions);
        // Every inline layout must be used directly or via another used layout.
        var pending = new Stack<ObjectTypeId>(program.Methods.SelectMany(m => m.Parameters.Concat(m.Locals).Append(m.ReturnType)
            .Concat(m.Blocks.SelectMany(b => b.Inputs)).Concat(m.Blocks.SelectMany(b => b.Values).Select(v => v.Type)))
            .Select(t => t.Kind == IrTypeKind.ManagedLocation ? t.Element! : t)
            .Where(t => t.Kind == IrTypeKind.Value).Select(t => t.Class!));
        foreach (var expression in program.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values).Select(v => v.Expression))
        {
            var box = expression switch { IrExpression.BoxValue b => b.Type, IrExpression.UnboxValue u => u.Type, IrExpression.UnboxLocation u => u.Type,
                IrExpression.IsInstance { BoxedTarget: true } t => t.Target, IrExpression.CastClass { BoxedTarget: true } t => t.Target, _ => null };
            if (box is not null) pending.Push(box);
        }
        foreach (var field in program.Types.References.Types.SelectMany(t => t.Fields).Select(f => f.Storage).OfType<ObjectStorage.Value>()) pending.Push(field.Type);
        foreach (var field in program.Types.References.Statics.Where(f => f.Type.Kind == IrTypeKind.Value)) pending.Push(field.Type.Class!);
        foreach (var array in program.Types.References.Arrays.Where(a => a.ArrayElement!.Kind == ObjectArrayElementKind.Value)) pending.Push(array.ArrayElement!.Type!);
        foreach (var argument in program.Types.References.Interfaces.SelectMany(i => i.Arguments)) if (argument.Value is { } value) pending.Push(value);
        foreach (var type in program.Types.References.Delegates.SelectMany(d => d.Parameters).Where(p => p.Kind == IrTypeKind.Value)) pending.Push(type.Class!);
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
        references = references.Concat(layouts.Values.SelectMany(t => t.Interfaces));
        if (objects is not null) references = references.Concat(program.Methods.SelectMany(objects.OperationTypes));
        if (exceptions is not null) references = references.Concat(exceptions.Types.Types);
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
    private readonly HashSet<ObjectTypeId> _boxTypes;
    internal ValueIrRules(ValueTypeCatalog catalog, bool locations = false, bool boxing = false)
    {
        Plans = ValueLayoutPlanner.Build(catalog);
        Catalog = catalog;
        _fields = catalog.Types.SelectMany(t => t.Fields).ToDictionary(f => f.Id);
        _boxTypes = boxing ? catalog.Types.Select(t => ObjectTypeId.From(t.Source)).ToHashSet() : [];
        if (locations) Locations = new(catalog, this);
    }

    private IrType BoxStorage(ObjectTypeId type) => Catalog.Types.First(t => ObjectTypeId.From(t.Source) == type).ScalarBox is { } scalar
        ? ScalarBoxing.Stack(scalar) : IrType.Value(type);

    internal bool Box(ObjectTypeId? type, IrType operand, IrType result) => type is not null
        && _boxTypes.Contains(type) && operand == BoxStorage(type)
        && result == IrType.Reference(Catalog.References.ObjectBase);

    internal bool TypeTest(ObjectTypeId? type, IrType result) => Catalog.References.Integer64 && type is not null
        && _boxTypes.Contains(type) && result == IrType.Reference(Catalog.References.ObjectBase);

    internal bool Unbox(ObjectTypeId? type, IrType operand, IrType result, bool borrow) => type is not null
        && _boxTypes.Contains(type) && operand.Kind is IrTypeKind.Reference or IrTypeKind.NullReference
        && (borrow ? Locations is not null && BoxStorage(type).Kind == IrTypeKind.Value && ManagedLocationRules.Address(BoxStorage(type), result)
            : result == BoxStorage(type));

    internal IrType? Field(IrType receiver, ObjectFieldId? id)
    {
        if (receiver.Kind != IrTypeKind.Value || id is null || receiver.Class != id.Owner || !_fields.TryGetValue(id, out var field)) return null;
        return field.Storage switch
        {
            ObjectStorage.Int32 => IrType.Int32,
            ObjectStorage.Int64 => IrType.Int64,
            ObjectStorage.Reference reference => IrType.Reference(reference.Type),
            ObjectStorage.Value value => IrType.Value(value.Type),
            _ => null,
        };
    }
    internal bool ReadOnly(ObjectFieldId id) => (_fields[id].Attributes & System.Reflection.FieldAttributes.InitOnly) != 0;
}
