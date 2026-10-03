using System;
using System.Collections.Generic;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Bounded assignment closure, built after identities and edge kinds have been checked.</summary>
internal sealed class ObjectTypeRelations
{
    internal Dictionary<ObjectTypeId, HashSet<ObjectTypeId>> Ancestors { get; } = [];

    internal ObjectTypeRelations(ObjectTypeCatalog catalog)
    {
        var children = new Dictionary<ObjectTypeId, List<ObjectTypeId>>();
        var remaining = new Dictionary<ObjectTypeId, int>();
        var ready = new Queue<ObjectTypeId>();
        void Add(ObjectTypeId id, IEnumerable<ObjectTypeId> parents)
        {
            Ancestors.Add(id, [id]); int count = 0;
            foreach (var parent in parents)
            {
                count++;
                if (!children.TryGetValue(parent, out var list)) children.Add(parent, list = []);
                list.Add(id);
            }
            remaining.Add(id, count);
            if (count == 0) ready.Enqueue(id);
        }
        foreach (var type in catalog.Types)
            Add(ObjectTypeId.From(type.Source), type.BaseType is null ? type.Interfaces : type.Interfaces.Prepend(type.BaseType));
        foreach (var contract in catalog.Interfaces)
            Add(ObjectTypeId.From(contract.Source), contract.Parents.Append(catalog.ObjectBase));
        if (catalog.String is { } text) Add(text, [catalog.ObjectBase]);
        int completed = 0;
        while (ready.TryDequeue(out var parent))
        {
            completed++;
            if (!children.TryGetValue(parent, out var dependents)) continue;
            foreach (var child in dependents)
            {
                Ancestors[child].UnionWith(Ancestors[parent]);
                if (--remaining[child] == 0) ready.Enqueue(child);
            }
        }
        if (completed != Ancestors.Count)
            throw new ArgumentException("Invalid object layout model: inheritance/interface cycle.");
        // Vector covariance depends only on the already validated nominal graph.
        // No element may itself be a vector in this profile, so no recursive closure is needed.
        foreach (var array in catalog.Arrays)
        {
            var ancestors = new HashSet<ObjectTypeId> { array, catalog.ObjectBase };
            if (array.ArrayElement!.Type is { } element)
                foreach (var target in catalog.Arrays)
                    if (target.ArrayElement!.Type is { } targetElement && Ancestors[element].Contains(targetElement))
                        ancestors.Add(target);
            Ancestors.Add(array, ancestors);
        }
    }
}
