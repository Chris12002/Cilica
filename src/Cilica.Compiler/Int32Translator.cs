using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

/// <summary>Translates the bounded int32-straight-line-v1 profile without executing input.</summary>
public static class Int32Translator
{
    public static TranslationResult Translate(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle method, MethodReadLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        var read = resolver.ReadMethodInput(assembly, method, limits);
        if (read.Value is not MethodInput input) return new(null, read.Diagnostics);
        return new Translation(input).Run();
    }

    private sealed class Translation(MethodInput input)
    {
        private readonly List<IrValue> _values = [];
        private readonly Stack<int> _stack = [];

        private TranslationResult Fail(string code, string message, int? offset = null) => new(null,
            [new(code, DiagnosticSeverity.Error, message,
                input.Source.Name + " token 0x" + input.Source.Token.ToString("x8", System.Globalization.CultureInfo.InvariantCulture)
                + (offset is int location ? " IL_" + location.ToString("x4", System.Globalization.CultureInfo.InvariantCulture) : ""))]);

        internal TranslationResult Run()
        {
            if ((input.Attributes & MethodAttributes.Static) == 0 || input.OwnerGenericArity != 0
                || input.Signature.GenericArity != 0 || input.Signature.Header != 0
                || input.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 }
                || input.Signature.Parameters.Any(p => p is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 }))
                return Fail(DiagnosticIds.ExecutionSignatureUnsupported,
                    "int32-straight-line-v1 requires a static nongeneric default-convention method with Int32 parameters and return type.");
            if (input.IsReferenceAssembly)
                return Fail(DiagnosticIds.ExecutionUnsupported, "Reference-assembly bodies cannot establish executable semantics; select an implementation image.");
            if ((input.Implementation & (MethodImplAttributes.CodeTypeMask | MethodImplAttributes.ManagedMask | MethodImplAttributes.InternalCall | MethodImplAttributes.Synchronized)) != MethodImplAttributes.IL
                || (input.Attributes & (MethodAttributes.PinvokeImpl | MethodAttributes.Abstract)) != 0)
                return Fail(DiagnosticIds.ExecutionUnsupported, "This execution profile requires ordinary managed IL without runtime/native bodies or synchronization.");
            if (input.HasTypeInitializer || input.HasModuleInitializer)
                return Fail(DiagnosticIds.ExecutionUnsupported, "Type/module initialization must be lowered before this method can be executed.");
            if (input.LocalCount != 0 || input.ExceptionRegionCount != 0)
                return Fail(DiagnosticIds.ExecutionUnsupported, "Locals and exception regions require later execution profiles.");
            if (input.Instructions.IsEmpty) return Fail(DiagnosticIds.InconsistentStack, "Method must return one Int32 value; the body is empty.", 0);
            IrReturn? returned = null;
            foreach (MethodInstruction instruction in input.Instructions)
            {
                int offset = instruction.Offset;
                if (returned is not null)
                    return Fail(DiagnosticIds.ExecutionUnsupported, "The straight-line profile requires one return at the end; trailing instructions require control-flow analysis.", offset);
                switch (instruction.OpCode)
                {
                    case "nop": break;
                    case "ldarg.0" or "ldarg.1" or "ldarg.2" or "ldarg.3" or "ldarg.s" or "ldarg":
                        if (instruction.IntegerOperand is not int argument || argument < 0 || argument >= input.Signature.Parameters.Count)
                            return Fail(DiagnosticIds.InvalidOperand, "Argument index is outside the selected method's parameters.", offset);
                        Push(offset, new IrExpression.Parameter(argument));
                        break;
                    case "ldc.i4.m1" or "ldc.i4.0" or "ldc.i4.1" or "ldc.i4.2" or "ldc.i4.3" or "ldc.i4.4" or "ldc.i4.5" or "ldc.i4.6" or "ldc.i4.7" or "ldc.i4.8":
                        int constant = instruction.OpCode == "ldc.i4.m1" ? -1 : instruction.OpCode[^1] - '0';
                        Push(offset, new IrExpression.Constant(constant));
                        break;
                    case "ldc.i4.s" or "ldc.i4":
                        Push(offset, new IrExpression.Constant(instruction.IntegerOperand!.Value));
                        break;
                    case "add" or "sub" or "mul":
                        if (_stack.Count < 2) return Fail(DiagnosticIds.InconsistentStack, "Int32 binary operation requires two Int32 values.", offset);
                        int right = _stack.Pop(), left = _stack.Pop();
                        Int32Operation operation = instruction.OpCode switch
                        {
                            "add" => Int32Operation.AddWrapping,
                            "sub" => Int32Operation.SubtractWrapping,
                            _ => Int32Operation.MultiplyWrapping,
                        };
                        Push(offset, new IrExpression.Binary(operation, left, right));
                        break;
                    case "ret":
                        if (_stack.Count != 1) return Fail(DiagnosticIds.InconsistentStack, "Int32 return requires exactly one value and no residual stack entries.", offset);
                        returned = new(offset, _stack.Pop());
                        break;
                    default:
                        return Fail(DiagnosticIds.ExecutionUnsupported,
                            "Instruction '" + instruction.OpCode + "' is outside int32-straight-line-v1; add a supported lowering before execution.", offset);
                }
                if (_stack.Count > input.MaxStack)
                    return Fail(DiagnosticIds.InconsistentStack, "Evaluation stack exceeds the method's declared maximum.", offset);
            }
            if (returned is null)
                return Fail(DiagnosticIds.InvalidControlTransfer, "The straight-line body falls through without returning an Int32 value.", input.CodeSize);
            return new(new(input.Source, [.. input.Signature.Parameters.Select(_ => IrType.Int32)], IrType.Int32,
                [new(0, [.. _values], returned)]), []);
        }

        private void Push(int offset, IrExpression expression)
        {
            int id = _values.Count;
            _values.Add(new(id, IrType.Int32, offset, expression));
            _stack.Push(id);
        }
    }
}
