using System;
using System.Collections.Generic;
using System.Linq;

namespace Cilica.Compiler;

// Box descriptors retain the nominal interface closure of their value payload.
// These relationships admit casts/storage, not boxed method dispatch.
internal sealed class BoxedTypeRelations
{
    private readonly Dictionary<ObjectTypeId, HashSet<ObjectTypeId>> _ancestors = [];
    internal BoxedTypeRelations(ValueTypeCatalog values) : this(values.References, values.Types) { }
    internal BoxedTypeRelations(ObjectTypeCatalog catalog, IEnumerable<ValueTypeLayout> values)
    {
        var references = new ObjectTypeRelations(catalog);
        int work = 0;
        foreach (var value in values)
        {
            var ancestors = new HashSet<ObjectTypeId> { catalog.ObjectBase };
            foreach (var contract in value.Interfaces)
                foreach (var parent in references.Ancestors[contract])
                {
                    if (++work > ObjectModelLimits.MaximumTypeRelationWork)
                        throw new ArgumentException("Boxed interface closure exceeds the type-relation work limit.");
                    ancestors.Add(parent);
                }
            _ancestors.Add(ObjectTypeId.From(value.Source), ancestors);
        }
    }
    internal bool Accepts(ObjectTypeId value, ObjectTypeId target) => _ancestors[value].Contains(target);

    internal void ValidateDispatch(IEnumerable<ObjectTypeId> boxedTypes, ObjectInterfaceDispatchPlan? dispatch)
    {
        if (dispatch is null) return;
        var used = boxedTypes.Distinct().ToArray();
        int work = 0;
        foreach (var owner in dispatch.Declarations.Select(d => d.Owner).Distinct())
            foreach (var type in used)
            {
                if (++work > ObjectModelLimits.MaximumTypeRelationWork)
                    throw new ArgumentException("Boxed dispatch closure exceeds the type-relation work limit.");
                if (Accepts(type, owner))
                    throw new ArgumentException("Boxed interface method dispatch requires payload receiver lowering before execution.");
            }
    }
}
