using System;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
namespace Cilica.Compiler;

// Authenticates the receiver conversion at both logical and scalar boundaries.
internal sealed class BoxedInterfacePlanRules
{
    private readonly ObjectInterfaceDispatchPlan _plan;
    private readonly Dictionary<ObjectTypeId, ValueTypeLayout> _values;
    private readonly Dictionary<ObjectTypeId, ValueTypePlan> _layouts;
    private readonly BoxedTypeRelations? _relations;
    private IrTypeRules? _signatureRules;
    internal BoxedInterfacePlanRules(ObjectInterfaceDispatchPlan plan)
    {
        _plan = plan;
        Require(plan.Values is null || plan.BoxedValues is null, "logical and scalar boxed catalogs are mutually exclusive.");
        var catalog = plan.Values ?? plan.BoxedValues;
        _values = catalog?.Types.Where(v => v.ScalarBox is null).ToDictionary(v => ObjectTypeId.From(v.Source)) ?? [];
        _layouts = catalog is null ? [] : ValueLayoutPlanner.Build(catalog).ToDictionary(p => p.Type);
        _relations = catalog is null ? null : new(catalog);
        if (plan.BoxedValues is not null)
            Require(plan.Types.Integer64 && ObjectInterfaceOperationRules.CatalogMatches(
                new HeapValueFields(catalog!, _layouts).Catalog, plan.Types), "scalar boxed dispatch catalog mismatch.");
    }
    internal IEnumerable<ObjectTypeId> Receivers(ObjectTypeId contract) =>
        _values.Keys.Where(v => _relations!.Accepts(v, contract));
    internal bool IsReceiver(ObjectTypeId type) => _values.ContainsKey(type);
    internal int ReceiverWidth(ObjectTypeId type) => _plan.BoxedValues is null ? 1 : _layouts[type].Slots.Length;
    private ImmutableArray<IrType> Receiver(ObjectTypeId type, bool readOnly) => _plan.BoxedValues is null
        ? [IrType.Location(IrType.Value(type), readOnly)]
        : [.. _layouts[type].Slots.Select(s => IrType.Location(s.Storage switch {
            ObjectStorage.Int32 => IrType.Int32, ObjectStorage.Int64 => IrType.Int64,
            ObjectStorage.Reference reference => IrType.Reference(reference.Type),
            _ => throw new ArgumentException("Invalid boxed interface scalar payload.") }, readOnly))];
    internal void Method(ObjectDispatchMethod method)
    {
        var original = method.BoxedDeclaration;
        Require(_plan.Types.Integer64 && !method.ArrayHelper && method.Binding is not null
            && original is not null && original.Method is not null && original.Owner is not null && _values.ContainsKey(original.Owner),
            "boxed dispatch requires an exact named non-scalar value owner.");
        var owner = _values[original.Owner];
        Require(original.Kind == ObjectMethodKind.ValueInstance && original.Method == method.Binding.Method
            && original.Method.ContentHash == original.Owner.ContentHash && original.Method.Specialization == original.Owner.Specialization
            && original.Method.Token >> 24 == 6 && (original.Method.Token & 0xffffff) != 0
            && original.GenericInterfaceMethod is null && original.VirtualSlot is null && original.VirtualName is null
            && original.Primitive is null && !original.InvariantProviderOnly && original.TypeInitializerName is null
            && original.OwnerExternallyVisible == owner.ExternallyVisible
            && ObjectLexicalAccess.Valid(original.Owner, original.LexicalOwners)
            && !string.IsNullOrEmpty(method.Name), "invalid boxed interface method provenance.");
        const MethodAttributes allowed = MethodAttributes.MemberAccessMask | MethodAttributes.HideBySig | MethodAttributes.SpecialName
            | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Final;
        var access = original.Attributes & MethodAttributes.MemberAccessMask;
        bool virtualMethod = (original.Attributes & MethodAttributes.Virtual) != 0;
        Require((original.Attributes & ~allowed) == 0
            && (access == MethodAttributes.Public || access == MethodAttributes.Private && virtualMethod
                && (original.Attributes & (MethodAttributes.Final | MethodAttributes.NewSlot)) == (MethodAttributes.Final | MethodAttributes.NewSlot))
            && (virtualMethod || (original.Attributes & (MethodAttributes.Final | MethodAttributes.NewSlot)) == 0),
            "boxed dispatch requires ordinary public or explicit private value methods.");
        var expected = _plan.BoxedValues is null ? original : ValueScalarLowering.LowerBindings([original])[0];
        Require(ObjectLexicalAccess.SameBinding(method.Binding, expected), "boxed dispatch lowered binding mismatch.");
        var receiver = Receiver(original.Owner, original.ReadOnlyReceiver);
        var rules = _signatureRules ??= new IrTypeRules(_plan.Types, _plan.Values, locations: true);
        Require(!method.Parameters.IsDefault && method.Parameters.Length <= IrLimits.MaximumParameters
            && method.Parameters.Length >= receiver.Length && method.Parameters.Take(receiver.Length).SequenceEqual(receiver)
            && method.Parameters.All(p => rules.Valid(p, true)) && method.ReturnType.Kind != IrTypeKind.ManagedLocation
            && (method.ReturnType == IrType.Void || rules.Valid(method.ReturnType, true)), "invalid boxed receiver packet.");
    }
    internal void Target(ObjectInterfaceDeclaration declaration, ObjectInterfaceTarget target, ObjectDispatchMethod method)
    {
        Require(declaration.GenericMethod is null && method.BoxedDeclaration is not null && method.Binding.Owner == target.Receiver
            && target.Mapping == target.Implementation && method.Binding.Method == target.Implementation
            && _relations!.Accepts(target.Receiver, declaration.Owner)
            && method.ReturnType == declaration.ReturnType
            && method.Parameters.Skip(ReceiverWidth(target.Receiver)).SequenceEqual(declaration.Parameters.Skip(1))
            && (target.IsExplicit ? (method.BoxedDeclaration.Attributes & MethodAttributes.Virtual) != 0
                : (method.BoxedDeclaration.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public && method.Name == declaration.Name),
            "boxed mapping must preserve its exact value owner, body and interface signature.");
    }
    private static void Require([DoesNotReturnIf(false)] bool condition, string message)
    { if (!condition) throw new ArgumentException("Invalid boxed interface plan: " + message); }
}
