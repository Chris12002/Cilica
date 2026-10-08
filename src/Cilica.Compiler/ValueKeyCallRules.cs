using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
namespace Cilica.Compiler;

internal sealed partial class ObjectIrRules
{
    private readonly ValueTypeCatalog? _valueKeyCatalog;
    private readonly bool _valueKeyScalar;

    private bool ValueKeyCall(IrMethod caller, IrMethodId target, ValueKeyCallContract proof)
    {
        if (!_program.Flow.Types.Integer64 || _valueKeyCatalog is null || proof.Contract is null || proof.Declaration is null
            || caller.Source.ContentHash != _program.Flow.Types.ObjectBase.ContentHash
            || !_bindings.TryGetValue(target, out var actual) || !_methods.TryGetValue(target, out var method)) return false;
        var original = proof.Declaration;
        var owner = _valueKeyCatalog.Types.SingleOrDefault(t => ObjectTypeId.From(t.Source) == original.Owner);
        if (owner is null || owner.ScalarBox is not null || original.Method != target || original.Kind != ObjectMethodKind.ValueInstance
            || original.OwnerExternallyVisible != owner.ExternallyVisible || original.TypeInitializerName is not null
            || original.Primitive is not null || original.IntegerParser is not null || original.ThrowingIntegerParser || original.InvariantProviderOnly
            || original.VirtualSlot is not null || original.VirtualName is not null || original.GenericInterfaceMethod is not null
            || !ObjectLexicalAccess.Valid(original.Owner, original.LexicalOwners)
            || !ObjectLexicalAccess.SameBinding(actual, _valueKeyScalar ? ValueScalarLowering.LowerBindings([original])[0] : original)
            || method.ReturnType != IrType.Int32) return false;
        const MethodAttributes allowed = MethodAttributes.MemberAccessMask | MethodAttributes.HideBySig
            | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Final;
        if ((original.Attributes & ~allowed) != 0) return false;
        var access = original.Attributes & MethodAttributes.MemberAccessMask;
        bool virtualMethod = (original.Attributes & MethodAttributes.Virtual) != 0;
        bool hash = proof.Contract == _valueKeyCatalog.References.ObjectBase;
        if (hash)
        {
            if (proof.IsExplicit || access != MethodAttributes.Public || !virtualMethod
                || (original.Attributes & MethodAttributes.NewSlot) != 0
                || method.Source.Name != owner.Source.Name + "::GetHashCode") return false;
        }
        else
        {
            var contract = _valueKeyCatalog.References.Interfaces.SingleOrDefault(t => ObjectTypeId.From(t.Source) == proof.Contract);
            if (contract is null || contract.Source.ContentHash != _valueKeyCatalog.References.ObjectBase.ContentHash
                || contract.Source.Name != "System.IEquatable`1" || !contract.ExternallyVisible
                || contract.Arguments.Length != 1 || contract.Arguments[0].Value != original.Owner
                || contract.Arguments[0].Variance != GenericParameterAttributes.None || !Implements(owner, proof.Contract)) return false;
            if (proof.IsExplicit)
            {
                if (!virtualMethod || access is not (MethodAttributes.Public or MethodAttributes.Private)
                    || (original.Attributes & (MethodAttributes.Final | MethodAttributes.NewSlot)) != (MethodAttributes.Final | MethodAttributes.NewSlot)) return false;
            }
            else if (access != MethodAttributes.Public || method.Source.Name != owner.Source.Name + "::Equals") return false;
        }
        ImmutableArray<IrType> fields = _valueKeyScalar
            ? [.. ValueLayoutPlanner.Build(_valueKeyCatalog).Single(p => p.Type == original.Owner).Slots.Select(s => s.Storage switch {
                ObjectStorage.Int32 => IrType.Int32, ObjectStorage.Int64 => IrType.Int64,
                ObjectStorage.Reference reference => IrType.Reference(reference.Type), _ => IrType.Void })]
            : [IrType.Value(original.Owner)];
        var expected = fields.Select(t => IrType.Location(t));
        if (!hash) expected = expected.Concat(fields);
        return !fields.Contains(IrType.Void) && method.Parameters.SequenceEqual(expected);
    }

    private bool Implements(ValueTypeLayout owner, ObjectTypeId contract)
    {
        var pending = new Stack<ObjectTypeId>(owner.Interfaces); var visited = new HashSet<ObjectTypeId>();
        while (pending.TryPop(out var current))
        {
            if (current == contract) return true;
            if (!visited.Add(current)) continue;
            var shape = _valueKeyCatalog!.References.Interfaces.SingleOrDefault(t => ObjectTypeId.From(t.Source) == current);
            if (shape is null) return false;
            foreach (var parent in shape.Parents) pending.Push(parent);
        }
        return false;
    }
}
