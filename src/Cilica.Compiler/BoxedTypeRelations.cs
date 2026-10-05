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

    internal void ValidateDispatch(IEnumerable<IrMethod> methods, ObjectInterfaceDispatchPlan? dispatch)
    {
        if (dispatch is null) return;
        var bodies = methods.ToArray(); var proof = new BoxedUseProof(bodies);
        var owners = dispatch.Declarations.Select(d => d.Owner).Distinct().ToArray();
        int work = 0;
        foreach (var method in bodies)
        foreach (var block in method.Blocks)
        foreach (var value in block.Values)
        {
            var type = value.Expression switch { IrExpression.BoxValue box => box.Type,
                IrExpression.BoxScalars box => box.Type, _ => null };
            if (type is null) continue;
            foreach (var owner in owners)
            {
                if (++work > ObjectModelLimits.MaximumTypeRelationWork)
                    throw new ArgumentException("Boxed dispatch closure exceeds the type-relation work limit.");
                if (Accepts(type, owner) && !proof.Safe(block, value.Id))
                    throw new ArgumentException("Boxed interface method dispatch requires payload receiver lowering before execution.");
            }
        }
    }
}
