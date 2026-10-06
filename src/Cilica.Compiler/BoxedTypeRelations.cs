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
    private readonly HashSet<ObjectTypeId> _scalarBoxes;
    internal BoxedTypeRelations(ValueTypeCatalog values) : this(values.References, values.Types) { }
    internal BoxedTypeRelations(ObjectTypeCatalog catalog, IEnumerable<ValueTypeLayout> values)
    {
        var layouts = values.ToArray();
        _interfaces = catalog.Interfaces.Select(t => ObjectTypeId.From(t.Source)).ToHashSet();
        _scalarBoxes = layouts.Where(t => t.ScalarBox is not null).Select(t => ObjectTypeId.From(t.Source)).ToHashSet();
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
        // Primitive contracts are incomplete. A cast from an unconstrained
        // reference to an interface therefore still needs scalar confinement.
        // An already interface-typed receiver has passed the IR's assignment or
        // cast checks: unrelated scalar allocations cannot flow into it without
        // an earlier object-to-interface conversion, which is checked here too.
        bool NeedsScalarFacts(IrBlock block)
        {
            var types = block.Values.ToDictionary(v => v.Id, v => v.Type);
            bool InterfaceReceiver(int operand) => types.TryGetValue(operand, out var type)
                && type.Kind == IrTypeKind.Reference && type.Class is not null && _interfaces.Contains(type.Class);
            return block.Values.Any(v => v.Expression switch {
                IrExpression.CastClass cast => _interfaces.Contains(cast.Target) && !InterfaceReceiver(cast.Operand),
                IrExpression.IsInstance test => _interfaces.Contains(test.Target) && !InterfaceReceiver(test.Operand), _ => false });
        }
        bool scalarInterfaceProof = _scalarBoxes.Count != 0 && bodies.SelectMany(m => m.Blocks).Any(NeedsScalarFacts);
        if (!scalarInterfaceProof && dispatch is null && classes.IsDefaultOrEmpty) return;
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
            if (scalarInterfaceProof && _scalarBoxes.Contains(type) && !proof.Safe(block, value.Id))
                throw new ArgumentException("Scalar box interface casts and dispatch require complete primitive/enum interface contracts or proven confined box uses before execution.");
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
