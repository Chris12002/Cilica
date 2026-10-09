using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;

/// <summary>A bare generic variable, or its single vector, at one reference type-test instruction.</summary>
public sealed record GenericTypeTestUse(int ILOffset, bool MethodParameter, int ParameterIndex, bool Vector, bool Cast);
public sealed record GenericTypeTestEvidence(ClosedGenericEvidence Context, ImmutableArray<GenericTypeTestUse> Uses);

internal sealed partial class ObjectIrRules
{
    private readonly Dictionary<(IrMethodId Method, int Offset), (ObjectTypeId Target, bool Cast)> _genericTypeTests = [];
    internal bool GenericTypeTest(IrMethod method, ObjectTypeId target, int offset, bool cast) =>
        _genericTypeTests.TryGetValue((IrMethodId.From(method.Source), offset), out var proof) && proof.Target == target && proof.Cast == cast;

    private void ValidateGenericTypeTests()
    {
        int work = 0, blocks = 0, statements = 0;
        foreach (var method in _program.Flow.Methods)
        {
            if (method.GenericTypeTests is not { } evidence) continue;
            Require(_program.Flow.Types.Integer64 && evidence.Context is not null
                && !evidence.Uses.IsDefaultOrEmpty && evidence.Uses.Length <= IrLimits.MaximumStatements,
                "generic type-test evidence requires the integer profile and bounded context/uses.");
            var id = IrMethodId.From(method.Source);
            Require(evidence.Context.Key(out int nodes) == id.Specialization && id.Specialization.Length != 0
                && new ClosedGenericEvidence(evidence.Context.Types, []).Key() == _bindings[id].Owner.Specialization,
                "generic type-test context contradicts the bound method or owner specialization.");
            work += nodes + evidence.Uses.Length;
            Require(work <= ObjectModelLimits.MaximumMetadataMembers, "aggregate generic type-test evidence exceeds the metadata work limit.");
            var operations = new Dictionary<int, (ObjectTypeId Target, bool Cast)>();
            Require(!method.Blocks.IsDefault && method.Blocks.Length <= IrLimits.MaximumBlocks - blocks,
                "generic type-test evidence requires bounded method blocks.");
            blocks += method.Blocks.Length;
            foreach (var block in method.Blocks)
            {
                Require(block is not null && !block.Statements.IsDefault && block.Statements.Length <= IrLimits.MaximumStatements - statements,
                    "generic type-test evidence requires bounded statement arrays.");
                statements += block.Statements.Length;
                foreach (var statement in block.Statements)
                {
                    if (statement is not IrStatement.Define {Value: {} value}) continue;
                    var operation = value.Expression switch {
                        IrExpression.IsInstance {BoxedTarget: false} test => (test.Target, false),
                        IrExpression.CastClass {BoxedTarget: false} test => (test.Target, true),
                        _ => ((ObjectTypeId?)null, false)
                    };
                    if (operation.Item1 is not null)
                        Require(operations.TryAdd(value.ILOffset, (operation.Item1, operation.Item2)), "generic type-test instruction has ambiguous operations.");
                }
            }
            foreach (var use in evidence.Uses)
            {
                Require(use is not null && use.ILOffset >= 0 && use.ParameterIndex >= 0,
                    "generic type-test use requires an instruction and parameter index.");
                var arguments = use.MethodParameter ? evidence.Context.Methods : evidence.Context.Types;
                Require(use.ParameterIndex < arguments.Length, "generic type-test variable index is out of range.");
                var argument = arguments[use.ParameterIndex];
                var target = GenericReferenceType(use.Vector ? new ClosedTypeShape.Vector(argument) : argument);
                Require(target is not null && _types.Valid(IrType.Reference(target), true)
                    && operations.TryGetValue(use.ILOffset, out var operation) && operation.Target == target && operation.Cast == use.Cast
                    && _genericTypeTests.TryAdd((id, use.ILOffset), (target, use.Cast)),
                    "generic type-test evidence contradicts or duplicates its instruction, operation or exact reference target.");
            }
        }
    }

    private ObjectTypeId? GenericReferenceType(ClosedTypeShape argument)
    {
        if (argument is ClosedTypeShape.Primitive {Code: PrimitiveTypeCode.Object}) return _program.Flow.Types.ObjectBase;
        if (argument is ClosedTypeShape.Primitive {Code: PrimitiveTypeCode.String}) return _program.Flow.Types.String;
        if (argument is ClosedTypeShape.Vector vector)
        {
            ObjectArrayElement? element = vector.Element is ClosedTypeShape.Primitive primitive ? primitive.Code switch {
                PrimitiveTypeCode.Int32 => ObjectArrayElement.Int32,
                PrimitiveTypeCode.UInt32 => ObjectArrayElement.UInt32,
                PrimitiveTypeCode.Int64 => ObjectArrayElement.Int64,
                PrimitiveTypeCode.Boolean => ObjectArrayElement.Boolean,
                PrimitiveTypeCode.Byte => ObjectArrayElement.Byte,
                PrimitiveTypeCode.Char => ObjectArrayElement.Char,
                PrimitiveTypeCode.Object => ObjectArrayElement.Reference(_program.Flow.Types.ObjectBase),
                PrimitiveTypeCode.String when _program.Flow.Types.String is {} text => ObjectArrayElement.Reference(text),
                _ => null
            } : GenericNominalArrayElement(vector.Element);
            if (element is null) return null;
            var array = ObjectTypeId.Vector(_program.Flow.Types.ObjectBase, element);
            return _arrays.Contains(array) ? array : null;
        }
        var nominal = GenericNominalArrayElement(argument);
        return nominal is {Kind: ObjectArrayElementKind.Reference} ? nominal.Type : null;
    }
}
