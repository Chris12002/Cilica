using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata.Resolution;

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
        foreach (var declaration in catalog.Delegates) Add(ObjectTypeId.From(declaration.Source), [catalog.ObjectBase]);
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
        if (catalog.Interfaces.IsEmpty) return;
        var candidates = catalog.Interfaces.Where(i => !i.Arguments.IsEmpty)
            .GroupBy(i => (i.Source.ContentHash, i.Source.Token))
            .SelectMany(instances => from source in instances from target in instances
                where source != target select (Source: source, Target: target)).ToArray();
        int work = 0;
        void Step()
        {
            if (++work > ObjectModelLimits.MaximumTypeRelationWork)
                throw new ArgumentException("Closed interface/array assignment closure exceeds the type-relation work limit.");
        }
        bool Compatible(ObjectInterface source, ObjectInterface target)
        {
            for (int index = 0; index < source.Arguments.Length; ++index)
            {
                Step(); var from = source.Arguments[index]; var to = target.Arguments[index];
                if (from.Identity == to.Identity) continue;
                if (from.Reference is null || to.Reference is null) return false;
                bool assigned = from.Variance switch {
                    GenericParameterAttributes.Covariant => Ancestors[from.Reference].Contains(to.Reference),
                    GenericParameterAttributes.Contravariant => Ancestors[to.Reference].Contains(from.Reference),
                    _ => false };
                if (!assigned) return false;
            }
            return true;
        }
        string integerArgument = ClosedGenericKey.Create(new([new TypeSignature.Primitive(PrimitiveTypeCode.Int32)]), SignatureList<TypeSignature>.Empty);
        string unsignedArgument = ClosedGenericKey.Create(new([new TypeSignature.Primitive(PrimitiveTypeCode.UInt32)]), SignatureList<TypeSignature>.Empty);
        // Monotone bounded fixed point: interface variance can enable class and
        // array conversions which in turn enable outer generic conversions.
        bool changed;
        do
        {
            changed = false;
            foreach (var (source, target) in candidates)
            {
                Step(); var from = Ancestors[ObjectTypeId.From(source.Source)]; var to = ObjectTypeId.From(target.Source);
                if (!from.Contains(to) && Compatible(source, target)) changed |= from.Add(to);
            }
            foreach (var array in catalog.Arrays)
                if (array.ArrayElement!.Type is { } element)
                    foreach (var target in catalog.Arrays)
                    {
                        Step();
                        if (target.ArrayElement!.Type is { } targetElement && Ancestors[element].Contains(targetElement))
                            changed |= Ancestors[array].Add(target);
                    }
            foreach (var array in catalog.Arrays)
                foreach (var contract in catalog.Interfaces.Where(i => i.ArrayContract != ObjectArrayInterfaceKind.None))
                {
                    Step();
                    bool compatible = contract.ArrayContract is ObjectArrayInterfaceKind.Enumerable or ObjectArrayInterfaceKind.NonGenericCollection or ObjectArrayInterfaceKind.NonGenericList;
                    if (!compatible)
                    {
                        var argument = contract.Arguments[0];
                        compatible = array.ArrayElement!.Type is { } element
                            ? argument.Reference is { } reference && Ancestors[element].Contains(reference)
                            : argument.Identity == integerArgument || argument.Identity == unsignedArgument;
                    }
                    if (compatible) changed |= Ancestors[array].Add(ObjectTypeId.From(contract.Source));
                }
            foreach (var ancestors in Ancestors.Values)
                foreach (var parent in ancestors.ToArray())
                    foreach (var ancestor in Ancestors[parent].ToArray())
                    {
                        Step(); changed |= ancestors.Add(ancestor);
                    }
        } while (changed);
    }
}
