using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Cilica.Compiler;

// Box descriptors retain the nominal interface closure of their value payload.
// These relationships admit casts/storage, not boxed method dispatch.
internal sealed class BoxedTypeRelations
{
    private readonly Dictionary<ObjectTypeId, HashSet<ObjectTypeId>> _ancestors = [];
    private readonly HashSet<ObjectTypeId> _interfaces;
    private readonly bool _hasScalarBoxes;
    internal BoxedTypeRelations(ValueTypeCatalog values) : this(values.References, values.Types) { }
    internal BoxedTypeRelations(ObjectTypeCatalog catalog, IEnumerable<ValueTypeLayout> values)
    {
        var layouts = values.ToArray();
        _interfaces = catalog.Interfaces.Select(t => ObjectTypeId.From(t.Source)).ToHashSet();
        _hasScalarBoxes = layouts.Any(t => t.ScalarBox is not null);
        var references = new ObjectTypeRelations(catalog);
        int work = 0;
        foreach (var value in layouts)
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

    internal void ValidateDispatch(IEnumerable<IrMethod> methods, ObjectInterfaceDispatchPlan? dispatch, ImmutableArray<ObjectVirtualDispatch> classes = default)
    {
        var bodies = methods.ToArray();
        // The bounded core does not yet declare every primitive's framework
        // interface contract. Do not turn that omission into a false type test.
        if (_hasScalarBoxes && (dispatch is not null && !dispatch.Declarations.IsEmpty
            || bodies.SelectMany(m => m.Blocks).SelectMany(b => b.Values).Any(v => v.Expression switch {
                IrExpression.CastClass cast => _interfaces.Contains(cast.Target),
                IrExpression.IsInstance test => _interfaces.Contains(test.Target), _ => false })))
            throw new ArgumentException("Scalar box interface casts and dispatch require complete primitive/enum interface contracts before execution.");
        if (dispatch is null && classes.IsDefaultOrEmpty) return;
        var proof = new BoxedUseProof(bodies);
        var byId = bodies.ToDictionary(m => IrMethodId.From(m.Source));
        var classOwners = classes.IsDefaultOrEmpty ? [] : classes.Select(d => byId[d.Declaration].Parameters[0].Class!).ToHashSet();
        var owners = (dispatch?.Declarations.Select(d => d.Owner) ?? []).Concat(classOwners).Distinct().ToArray();
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
                    throw new ArgumentException(classOwners.Contains(owner)
                        ? "Boxed class method dispatch requires payload receiver lowering before execution."
                        : "Boxed interface method dispatch requires payload receiver lowering before execution.");
            }
        }
    }
}
