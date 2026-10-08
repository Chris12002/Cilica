using System.Collections.Generic;
using System.Linq;
namespace Cilica.Compiler;

// Declared interface ancestry is distinct from assignability through variance.
// Exact contracts win; this bounded profile admits only one variant candidate.
internal sealed class ObjectInterfaceVarianceRules(ObjectTypeCatalog catalog, IrTypeRules types, ValueTypeCatalog? values = null)
{
    private readonly Dictionary<ObjectTypeId, ObjectTypeLayout> _classes = catalog.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
    private readonly Dictionary<ObjectTypeId, ValueTypeLayout> _values = values?.Types.Where(v => v.ScalarBox is null).ToDictionary(v => ObjectTypeId.From(v.Source)) ?? [];
    private readonly Dictionary<ObjectTypeId, ObjectInterface> _contracts = catalog.Interfaces.ToDictionary(t => ObjectTypeId.From(t.Source));
    private readonly Dictionary<ObjectTypeId, HashSet<ObjectTypeId>> _nominal = [];
    internal HashSet<ObjectTypeId> Nominal(ObjectTypeId receiver)
    {
        if (_nominal.TryGetValue(receiver, out var known)) return known;
        var result = new HashSet<ObjectTypeId>(); var pending = new Stack<ObjectTypeId>();
        if (_values.TryGetValue(receiver, out var value))
            foreach (var contract in value.Interfaces) pending.Push(contract);
        else for (ObjectTypeId? owner = receiver; owner is not null; owner = _classes[owner].BaseType)
            foreach (var contract in _classes[owner].Interfaces) pending.Push(contract);
        while (pending.TryPop(out var contract))
            if (result.Add(contract)) foreach (var parent in _contracts[contract].Parents) pending.Push(parent);
        _nominal.Add(receiver, result); return result;
    }
    internal ObjectTypeId[] Candidates(ObjectTypeId receiver, ObjectTypeId requested) =>
        [.. Nominal(receiver).Where(id => id != requested && id.ContentHash == requested.ContentHash && id.Token == requested.Token
            && types.Assignable(IrType.Reference(id), IrType.Reference(requested)))];
    internal static bool Packet(ObjectInterfaceDeclaration requested, ObjectInterfaceDeclaration selected, IrTypeRules types) =>
        requested.Parameters.Length == selected.Parameters.Length
        && requested.Parameters.Skip(1).Zip(selected.Parameters.Skip(1)).All(p => Transfer(p.First, p.Second, types))
        && Transfer(selected.ReturnType, requested.ReturnType, types);
    private static bool Transfer(IrType source, IrType destination, IrTypeRules types) =>
        source.Kind is IrTypeKind.Reference or IrTypeKind.Int32 or IrTypeKind.Int64 or IrTypeKind.Void
        && destination.Kind is IrTypeKind.Reference or IrTypeKind.Int32 or IrTypeKind.Int64 or IrTypeKind.Void
        && (source == destination || source.Kind == IrTypeKind.Reference && destination.Kind == IrTypeKind.Reference && types.Assignable(source, destination));
}
