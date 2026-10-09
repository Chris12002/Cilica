using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Cilica.Compiler;

// Box descriptors retain the nominal interface closure of their value payload.
// Named payload dispatch requires separately validated complete interface maps.
internal sealed class BoxedTypeRelations
{
    private readonly Dictionary<ObjectTypeId, HashSet<ObjectTypeId>> _ancestors = [];
    private readonly HashSet<ObjectTypeId> _interfaces;
    private readonly HashSet<ObjectTypeId> _scalarBoxes;
    private readonly ObjectTypeId _object;
    internal BoxedTypeRelations(ValueTypeCatalog values) : this(values.References, values.Types) { }
    internal BoxedTypeRelations(ObjectTypeCatalog catalog, IEnumerable<ValueTypeLayout> values)
    {
        _object = catalog.ObjectBase;
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

    internal void ValidateDispatch(IEnumerable<IrMethod> methods, ObjectInterfaceDispatchPlan? dispatch, ImmutableArray<ObjectVirtualDispatch> classes = default, ImmutableArray<ObjectAbstractMethodDeclaration> abstractMethods = default)
    {
        var bodies = methods.ToArray();
        // Primitive contracts are incomplete. A cast from an unconstrained
        // reference to an interface therefore still needs scalar confinement.
        // An already interface-typed receiver has passed the IR's assignment or
        // cast checks: unrelated scalar allocations cannot flow into it without
        // an earlier object-to-interface conversion, which is checked here too.
        bool NeedsScalarFacts(IrBlock block)
        {
            var values = block.Values.ToDictionary(v => v.Id);
            bool ExcludesUnknownScalar(int operand)
            {
                if (!values.TryGetValue(operand, out var value)) return false;
                // Checked nominal references cannot contain scalar boxes, whose
                // only class ancestor in this profile is Object. Interface views
                // already passed the assignment/cast checks at their origin.
                if (value.Type.Kind == IrTypeKind.Reference && value.Type.Class != _object) return true;
                var boxed = value.Expression switch { IrExpression.BoxValue box => box.Type,
                    IrExpression.BoxScalars box => box.Type, _ => null };
                return boxed is not null && _ancestors.ContainsKey(boxed) && !_scalarBoxes.Contains(boxed);
            }
            return block.Values.Any(v => v.Expression switch {
                IrExpression.CastClass cast => _interfaces.Contains(cast.Target) && !ExcludesUnknownScalar(cast.Operand),
                IrExpression.IsInstance test => _interfaces.Contains(test.Target) && !ExcludesUnknownScalar(test.Operand), _ => false });
        }
        bool scalarInterfaceProof = _scalarBoxes.Count != 0 && bodies.SelectMany(m => m.Blocks).Any(NeedsScalarFacts);
        if (!scalarInterfaceProof && dispatch is null && classes.IsDefaultOrEmpty) return;
        var proof = new BoxedUseProof(bodies);
        var byId = bodies.ToDictionary(m => IrMethodId.From(m.Source));
        if (!abstractMethods.IsDefault) foreach (var declaration in abstractMethods) byId.Add(IrMethodId.From(declaration.Source), declaration.Signature);
        // A class-slot declaration on Object does not mean a box can reach
        // every call site. Use the checked static receiver type at each call;
        // ordinary class references cannot contain boxes. Object aliases retain
        // the conservative Object owner and the existing payload rejection.
        var classDeclarations = classes.IsDefaultOrEmpty ? [] : classes.Select(d => d.Declaration).ToHashSet();
        var classOwners = new HashSet<ObjectTypeId>();
        foreach (var method in bodies)
        foreach (var block in method.Blocks)
        {
            var types = block.Values.ToDictionary(v => v.Id, v => v.Type);
            void Receiver(IrMethodId target, ImmutableArray<int> arguments, bool virtualDispatch)
            {
                if (!virtualDispatch || !classDeclarations.Contains(target)) return;
                var owner = byId[target].Parameters[0].Class!;
                if (!arguments.IsEmpty && types.TryGetValue(arguments[0], out var receiver))
                {
                    if (receiver.Kind == IrTypeKind.NullReference) return;
                    if (receiver.Kind == IrTypeKind.Reference && receiver.Class is { } exact) owner = exact;
                }
                classOwners.Add(owner);
            }
            foreach (var value in block.Values)
                if (value.Expression is IrExpression.Call call) Receiver(call.Target, call.Arguments, call.VirtualDispatch);
            foreach (var statement in block.Statements)
                if (statement is IrStatement.Invoke call) Receiver(call.Target, call.Arguments, call.VirtualDispatch);
        }
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
                // Keep the work charge for every pair, but do not inspect dispatch
                // tables for an owner this payload cannot implement. Both callers
                // already validated complete dispatch maps before reaching here.
                if (!Accepts(type, owner)) continue;
                bool payloadDispatch = !_scalarBoxes.Contains(type) && !classOwners.Contains(owner) && dispatch is not null
                    && dispatch.Declarations.Where(d => d.Owner == owner).All(d => dispatch.Dispatch
                        .Single(m => m.Declaration == IrMethodId.From(d.Source)).Targets.Any(t => t.Receiver == type));
                if (!proof.Safe(block, value.Id) && !payloadDispatch)
                    throw new ArgumentException(classOwners.Contains(owner)
                        ? "Boxed class method dispatch requires payload receiver lowering before execution."
                        : "Boxed interface method dispatch requires payload receiver lowering before execution.");
            }
        }
    }
}
