namespace Cilica.Compiler;

/// <summary>Structural proofs for native paths that cannot allocate or call managed code.</summary>
internal static class ObjectCppLeafMethods
{
    internal static ObjectFieldId? FieldGetter(IrMethod method)
    {
        // Inspect validated operations, never names or compiler-generated attributes.
        // Exclude locals, control flow, handlers and scalarized multi-field results.
        if (method.Parameters.Length != 1 || method.Parameters[0].Kind != IrTypeKind.Reference
            || method.ReturnType.Kind is not (IrTypeKind.Int32 or IrTypeKind.Int64 or IrTypeKind.Reference)
            || !method.Locals.IsEmpty || !method.ExceptionRegions.IsEmpty || method.Blocks.Length != 1)
            return null;
        var block = method.Blocks[0];
        if (!block.Inputs.IsEmpty || block.Statements.Length != 2
            || block.Statements[0] is not IrStatement.Define { Value: var receiver }
            || receiver.Expression is not IrExpression.Parameter { Index: 0 }
            || receiver.Type != method.Parameters[0]
            || block.Statements[1] is not IrStatement.Define { Value: var result }
            || result.Expression is not IrExpression.LoadField field || field.Receiver != receiver.Id
            || result.Type != method.ReturnType
            || block.Terminator is not IrReturn returned || returned.Value != result.Id)
            return null;
        return field.Field;
    }
}
