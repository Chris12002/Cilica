using System;
using System.Collections.Generic;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Shared closure check after layout and IR validation: declared roots cannot hide unused types.</summary>
internal static class ObjectCatalogUsage
{
    internal static void Validate(ObjectTypeCatalog catalog, IEnumerable<ObjectTypeId> roots)
    {
        var layouts = catalog.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
        var interfaces = catalog.Interfaces.ToDictionary(t => ObjectTypeId.From(t.Source));
        var arrays = catalog.Arrays.ToHashSet();
        var pending = new Stack<ObjectTypeId>(roots); pending.Push(catalog.ObjectBase);
        var visited = new HashSet<ObjectTypeId>();
        while (pending.TryPop(out var id))
            if (visited.Add(id))
            {
                if (id == catalog.String) pending.Push(catalog.ObjectBase);
                else if (arrays.Contains(id))
                {
                    if (id.ArrayElement!.Type is { } element) pending.Push(element);
                }
                else if (interfaces.TryGetValue(id, out var contract))
                    foreach (var parent in contract.Parents) pending.Push(parent);
                else
                {
                    if (layouts[id].BaseType is { } parent) pending.Push(parent);
                    foreach (var edge in layouts[id].Interfaces) pending.Push(edge);
                    foreach (var reference in layouts[id].Fields.Select(f => f.Storage).OfType<ObjectStorage.Reference>()) pending.Push(reference.Type);
                }
            }
        if (visited.Count != layouts.Count + interfaces.Count + arrays.Count + (catalog.String is null ? 0 : 1))
            throw new ArgumentException("Invalid reference program IR: catalog contains types unused by value flow or its field closure.", nameof(catalog));
    }
}
