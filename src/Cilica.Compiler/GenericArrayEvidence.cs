using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Linq;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

/// <summary>Original bare generic-variable array use, scoped to one IL instruction.</summary>
public sealed record GenericArrayUse(int ILOffset, bool MethodParameter, int ParameterIndex, bool Empty);
public sealed record GenericArrayEvidence(ClosedGenericEvidence Context, ImmutableArray<GenericArrayUse> Uses);

internal sealed partial class ObjectIrRules
{
    private readonly Dictionary<(IrMethodId Method, int Offset), ObjectTypeId> _genericArrays = [];

    private void ValidateGenericArrays()
    {
        int work = 0, blocks = 0, statements = 0;
        foreach (var method in _program.Flow.Methods)
        {
            if (method.GenericArrays is not { } evidence) continue;
            Require(_program.Flow.Types.Integer64 && evidence.Context is not null
                && !evidence.Uses.IsDefaultOrEmpty && evidence.Uses.Length <= IrLimits.MaximumStatements,
                "generic array evidence requires the integer profile and bounded context/uses.");
            var id = IrMethodId.From(method.Source);
            Require(evidence.Context.Key(out int nodes) == id.Specialization && id.Specialization.Length != 0
                && new ClosedGenericEvidence(evidence.Context.Types, []).Key() == _bindings[id].Owner.Specialization,
                "generic array context contradicts the bound method or owner specialization.");
            work += nodes + evidence.Uses.Length;
            Require(work <= ObjectModelLimits.MaximumMetadataMembers, "aggregate generic array evidence exceeds the metadata work limit.");
            var operations = new Dictionary<int, (ObjectTypeId Array, bool Empty)>();
            Require(!method.Blocks.IsDefault && method.Blocks.Length <= IrLimits.MaximumBlocks - blocks,
                "generic array evidence requires bounded method blocks.");
            blocks += method.Blocks.Length;
            foreach (var block in method.Blocks)
            {
                Require(block is not null && !block.Statements.IsDefault
                    && block.Statements.Length <= IrLimits.MaximumStatements - statements,
                    "generic array evidence requires bounded statement arrays.");
                statements += block.Statements.Length;
                foreach (var statement in block.Statements)
                {
                    if (statement is not IrStatement.Define { Value: { } value }) continue;
                    var operation = value.Expression switch {
                        IrExpression.NewArray array => (array.ArrayType, false),
                        IrExpression.EmptyArray array => (array.ArrayType, true),
                        _ => ((ObjectTypeId?)null, false) };
                    if (operation.Item1 is not null)
                        Require(operations.TryAdd(value.ILOffset, (operation.Item1, operation.Item2)), "generic array instruction has ambiguous operations.");
                }
            }
            foreach (var use in evidence.Uses)
            {
                Require(use is not null && use.ILOffset >= 0 && use.ParameterIndex >= 0,
                    "generic array use requires an instruction and parameter index.");
                var arguments = use.MethodParameter ? evidence.Context.Methods : evidence.Context.Types;
                Require(use.ParameterIndex < arguments.Length, "generic array variable index is out of range.");
                var storage = GenericNominalArrayElement(arguments[use.ParameterIndex]);
                Require(storage is not null, "generic array evidence needs an exact admitted reference, inline value or Int32 enum argument.");
                var array = ObjectTypeId.Vector(_program.Flow.Types.ObjectBase, storage);
                Require(_arrays.Contains(array) && operations.TryGetValue(use.ILOffset, out var operation)
                    && operation.Array == array && operation.Empty == use.Empty
                    && _genericArrays.TryAdd((id, use.ILOffset), array),
                    "generic array evidence contradicts or duplicates its instruction, operation or element identity.");
            }
        }
    }
    private ObjectArrayElement? GenericNominalArrayElement(ClosedTypeShape argument)
    {
        ObjectTypeId? element = argument switch {
            ClosedTypeShape.Named named => new(named.ContentHash, named.Token),
            ClosedTypeShape.Generic generic => new(generic.Definition.ContentHash,
                generic.Definition.Token, Specialization: new ClosedGenericEvidence(generic.Arguments, []).Key()),
            _ => null };
        var kind = argument switch {
            ClosedTypeShape.Named named => named.Kind,
            ClosedTypeShape.Generic generic => generic.Definition.Kind,
            _ => (SignatureTypeKind?)null };

        if (element is not null && kind == SignatureTypeKind.Class && _types.Valid(IrType.Reference(element), true))
            return ObjectArrayElement.Reference(element);
        else if (element is not null && kind == SignatureTypeKind.ValueType)
        {
            if (_program.Flow.Types.EnumArrays.Any(e => ObjectTypeId.From(e.Source) == element))
                return ObjectArrayElement.Enum32(element);
            else if (_arrayValueLayouts?.TryGetValue(element, out var value) == true && value.ScalarBox is null)
                return ObjectArrayElement.Value(element);
        }
        return null;
    }

}
