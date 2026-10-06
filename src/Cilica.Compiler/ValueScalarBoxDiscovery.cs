using System.Reflection.Metadata;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private ObjectTypeId? BoxedTypeTestOperand(TypeSignature signature, Work work, int offset)
        {
            bool nullable = IsNullable(signature, work, offset);
            if (nullable)
            {
                CheckArgumentAccess(signature, work, offset);
                var closed = (TypeSignature.Generic)signature;
                if (closed.Arguments.Count != 1) Fail(work, "Nullable type tests require exactly one closed payload argument.", offset);
                // Validate arity, struct constraints and metadata bounds even when
                // no Nullable<T> storage is otherwise used by this closure.
                _ = SnapshotClosed(new(closed.Definition.Definition, closed.Arguments), work, offset);
                signature = closed.Arguments[0];
            }
            var normalized = OperandSignature(signature, work, offset);
            bool valueTarget = normalized is TypeSignature.Primitive primitive && ScalarBoxing.Supported(primitive.Code)
                || normalized is TypeSignature.Named { Kind: SignatureTypeKind.ValueType }
                    or TypeSignature.Generic { Definition.Kind: SignatureTypeKind.ValueType };
            if (!valueTarget)
            {
                if (nullable) Fail(work, "Nullable type tests require an admitted scalar or named value payload.", offset);
                return null;
            }
            if (ScalarBoxOperand(normalized, work, offset) is { } scalar) return scalar.Type;
            return Storage(normalized, work, offset).Class!;
        }

        private (ObjectTypeId Type, PrimitiveTypeCode Code)? ScalarBoxOperand(TypeSignature signature, Work work, int offset)
        {
            CheckArgumentAccess(signature, work, offset);
            var normalized = OperandSignature(signature, work, offset);
            ResolvedType definition;
            PrimitiveTypeCode code;
            if (normalized is TypeSignature.Primitive primitive && ScalarBoxing.Supported(primitive.Code))
            {
                code = primitive.Code;
                definition = Require(resolver.ResolveRuntimeType(library, code), work, offset);
            }
            else if (EnumScalar(normalized, work, offset))
            {
                definition = ((TypeSignature.Named)normalized).Definition; code = PrimitiveTypeCode.Int32;
            }
            else return null;
            var id = Include(definition, work, offset);
            _scalarBoxes.TryAdd(id, code);
            return (id, code);
        }
    }
}
