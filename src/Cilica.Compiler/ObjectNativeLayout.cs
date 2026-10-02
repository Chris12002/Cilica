using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Bounds physical field copies after composing every concrete class.</summary>
public static class ObjectNativeLimits
{
    public const int MaximumFieldSlots = 65536;
}

/// <summary>Base-first fields; each declaration's index is stable in all descendants.</summary>
internal sealed class ObjectNativeLayout
{
    internal Dictionary<ObjectTypeId, ImmutableArray<ObjectField>> Fields { get; } = [];
    internal Dictionary<ObjectFieldId, int> Indices { get; } = [];

    internal ObjectNativeLayout(ObjectTypeCatalog catalog)
    {
        // The caller has validated the graph. Avoid recursion even at the type limit.
        var types = catalog.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
        int slots = 0;
        foreach (var id in types.Keys.OrderBy(t => t.ContentHash, StringComparer.Ordinal).ThenBy(t => t.Token))
        {
            var pending = new Stack<ObjectTypeId>();
            for (ObjectTypeId? current = id; current is not null && !Fields.ContainsKey(current); current = types[current].BaseType)
                pending.Push(current);
            while (pending.TryPop(out var current))
            {
                var type = types[current];
                var inherited = type.BaseType is null ? ImmutableArray<ObjectField>.Empty : Fields[type.BaseType];
                int count = inherited.Length + type.Fields.Length;
                if (count > ObjectNativeLimits.MaximumFieldSlots - slots)
                    throw new ArgumentException("Inherited native field-slot limit exceeded.");
                slots += count;
                Fields.Add(current, [.. inherited, .. type.Fields]);
                for (int index = 0; index < type.Fields.Length; index++)
                    Indices.Add(type.Fields[index].Id, inherited.Length + index);
            }
        }
    }
}
