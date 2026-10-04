using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

/// <summary>Translates bounded Int32 methods without executing input.</summary>
public static partial class Int32Translator
{
    public static TranslationResult Translate(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle method, MethodReadLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        var read = resolver.ReadMethodInput(assembly, method, limits);
        if (read.Value is not MethodInput input) return new(null, read.Diagnostics);
        try { return new(new Translation(input).Run(), []); }
        catch (TranslationFailure failure) { return new(null, [failure.Diagnostic]); }
    }

    private sealed class TranslationFailure(Diagnostic diagnostic) : Exception(diagnostic.Message)
    {
        internal Diagnostic Diagnostic { get; } = diagnostic;
    }

    private sealed class Translation(MethodInput input, Func<MethodInstruction, ScalarCall>? resolveCall = null, bool integers64 = false)
    {
        private readonly Dictionary<int, int> _blockAt = [];
        private readonly Queue<int> _pending = new();
        private readonly List<(int Start, int End)> _ranges = [];
        private ImmutableArray<IrType>?[] _stacks = [];
        private int _statements, _inputs, _edgeArguments;

        private void Fail(string code, string message, int offset = 0) => throw new TranslationFailure(
            new(code, DiagnosticSeverity.Error, message, input.Source.Name + " token 0x" +
                input.Source.Token.ToString("x8", System.Globalization.CultureInfo.InvariantCulture) +
                " IL_" + offset.ToString("x4", System.Globalization.CultureInfo.InvariantCulture)));

        internal IrMethod Run()
        {
            bool Scalar(TypeSignature type) => ScalarType(type, integers64) is not null;
            if ((input.Attributes & MethodAttributes.Static) == 0 || input.OwnerGenericArity != 0
                || input.Signature.GenericArity != 0 || input.Signature.Header != 0
                || !Scalar(input.Signature.ReturnType) || input.Signature.Parameters.Any(p => !Scalar(p))
                || input.Locals.Any(l => !Scalar(l)))
                Fail(DiagnosticIds.ExecutionSignatureUnsupported, integers64
                    ? IntegerTranslator.Profile + " requires static nongeneric Int32/UInt32/Int64/UInt64 parameters, locals and return."
                    : IrMethod.Profile + " requires a static nongeneric default-convention method with Int32 parameters, locals and return type.");
            if (input.Signature.Parameters.Count > IrLimits.MaximumParameters || input.Locals.Length > IrLimits.MaximumLocals)
                Fail(DiagnosticIds.TranslationLimit, "Parameter/local storage limit exceeded.");
            if (input.IsReferenceAssembly)
                Fail(DiagnosticIds.ExecutionUnsupported, "Reference-assembly bodies cannot establish executable semantics; select an implementation image.");
            if ((input.Implementation & (MethodImplAttributes.CodeTypeMask | MethodImplAttributes.ManagedMask | MethodImplAttributes.InternalCall | MethodImplAttributes.Synchronized)) != MethodImplAttributes.IL
                || (input.Attributes & (MethodAttributes.PinvokeImpl | MethodAttributes.Abstract)) != 0)
                Fail(DiagnosticIds.ExecutionUnsupported, "This execution profile requires ordinary managed IL without runtime/native bodies or synchronization.");
            if (input.HasTypeInitializer || input.HasModuleInitializer)
                Fail(DiagnosticIds.ExecutionUnsupported, "Type/module initialization must be lowered before this method can be executed.");
            if (input.ExceptionRegionCount != 0 || (!input.Locals.IsEmpty && !input.InitializeLocals))
                Fail(DiagnosticIds.ExecutionUnsupported, "Exception regions and uninitialized locals require later execution profiles.");
            if (input.Instructions.IsEmpty) Fail(DiagnosticIds.InconsistentStack, "Method body is empty.");
            Partition();
            _stacks = new ImmutableArray<IrType>?[_ranges.Count]; _stacks[0] = []; _pending.Enqueue(0);
            var blocks = new IrBlock[_ranges.Count];
            while (_pending.TryDequeue(out int id)) blocks[id] = TranslateBlock(id, _stacks[id]!.Value);
            for (int id = 0; id < blocks.Length; id++)
                if (blocks[id] is null) Fail(DiagnosticIds.ExecutionUnsupported,
                    "Unreachable blocks are outside this verified profile; remove dead IL or add unreachable-block analysis.", input.Instructions[_ranges[id].Start].Offset);
            return new(input.Source, [.. input.Signature.Parameters.Select(p => ScalarType(p, integers64)!)], ScalarType(input.Signature.ReturnType, integers64)!,
                [.. blocks], [.. input.Locals.Select(l => ScalarType(l, integers64)!)]);
        }

        private void Partition()
        {
            var partition = CilControlFlow.Partition(input, Fail);
            _ranges.AddRange(partition.Ranges);
            foreach (var pair in partition.BlockAt) _blockAt.Add(pair.Key, pair.Value);
        }

        private IrBlock TranslateBlock(int id, ImmutableArray<IrType> incoming)
        {
            int depth = incoming.Length;
            var (start, end) = _ranges[id]; int blockOffset = input.Instructions[start].Offset;
            if (depth > input.MaxStack) Fail(DiagnosticIds.InconsistentStack, "Incoming stack exceeds the declared maximum.", blockOffset);
            _inputs += depth;
            if (_inputs > IrLimits.MaximumInputs) Fail(DiagnosticIds.TranslationLimit, "Aggregate block-input limit exceeded.", blockOffset);
            var statements = new List<IrStatement>(); var stack = new List<int>(); var valueTypes = new List<IrType>(); int values = 0;
            IrTerminator? terminator = null;
            for (int index = 0; index < depth; index++) Push(blockOffset, new IrExpression.StackInput(index), type: incoming[index]);
            for (int index = start; index < end; index++)
            {
                var instruction = input.Instructions[index]; int offset = instruction.Offset;
                string opcode = instruction.OpCode;
                switch (opcode)
                {
                    case "nop": break;
                    case "ldarg.0" or "ldarg.1" or "ldarg.2" or "ldarg.3" or "ldarg.s" or "ldarg":
                        int parameter = Variable(instruction, input.Signature.Parameters.Count, "Argument");
                        Push(offset, new IrExpression.Parameter(parameter), type: ScalarType(input.Signature.Parameters[parameter], integers64)); break;
                    case "ldloc.0" or "ldloc.1" or "ldloc.2" or "ldloc.3" or "ldloc.s" or "ldloc":
                        int loaded = Variable(instruction, input.Locals.Length, "Local");
                        Push(offset, new IrExpression.Local(loaded), type: ScalarType(input.Locals[loaded], integers64)); break;
                    case "stloc.0" or "stloc.1" or "stloc.2" or "stloc.3" or "stloc.s" or "stloc":
                        int local = Variable(instruction, input.Locals.Length, "Local");
                        Add(new IrStatement.StoreLocal(offset, local, Pop(offset, ScalarType(input.Locals[local], integers64))), offset); break;
                    case "ldc.i4.m1" or "ldc.i4.0" or "ldc.i4.1" or "ldc.i4.2" or "ldc.i4.3" or "ldc.i4.4" or "ldc.i4.5" or "ldc.i4.6" or "ldc.i4.7" or "ldc.i4.8":
                        Push(offset, new IrExpression.Constant(opcode == "ldc.i4.m1" ? -1 : opcode[^1] - '0')); break;
                    case "ldc.i4.s" or "ldc.i4": Push(offset, new IrExpression.Constant(instruction.IntegerOperand!.Value)); break;
                    case "ldc.i8" when integers64:
                        if (instruction.Int64Operand is null) Fail(DiagnosticIds.InvalidOperand, "ldc.i8 requires a complete Int64 operand.", offset);
                        Push(offset, new IrExpression.Constant64(instruction.Int64Operand!.Value), type: IrType.Int64); break;
                    case "conv.i8" or "conv.u8" or "conv.i4" or "conv.u4" when integers64:
                        int converted = Pop(offset); bool toWide = opcode is "conv.i8" or "conv.u8";
                        var convertedType = toWide ? IrType.Int64 : IrType.Int32;
                        if (valueTypes[converted] == convertedType) stack.Add(converted);
                        else Push(offset, new IrExpression.ConvertInteger(toWide
                            ? opcode == "conv.i8" ? IntegerConversion.SignExtend32 : IntegerConversion.ZeroExtend32
                            : IntegerConversion.Truncate64, converted), type: convertedType);
                        break;
                    case "dup": int duplicated = Pop(offset); stack.Add(duplicated); stack.Add(duplicated); break;
                    case "pop": Pop(offset); break;
                    case "add" or "sub" or "mul" or "and" or "or" or "xor" or "shl" or "shr" or "shr.un" or "div" or "div.un"
                        or "add.ovf" or "sub.ovf" or "mul.ovf" or "add.ovf.un" or "sub.ovf.un" or "mul.ovf.un" or "rem" or "rem.un":
                        int right = Pop(offset), left = Pop(offset);
                        Int32Operation operation = Int32Operations.FromOpcode(opcode)
                            ?? throw new InvalidOperationException("Supported binary instruction has no lowering.");
                        bool shift = operation is Int32Operation.ShiftLeft or Int32Operation.ShiftRightSigned or Int32Operation.ShiftRightUnsigned;
                        if (valueTypes[right] != (shift ? IrType.Int32 : valueTypes[left]))
                            Fail(DiagnosticIds.InconsistentStack, "Arithmetic requires matching integer widths, with Int32 shift counts.", offset);
                        if (valueTypes[left] == IrType.Int64)
                            Push(offset, new IrExpression.Binary64(Int64Operations.FromOpcode(opcode), left, right),
                                Int32Operations.MayFault(operation) ? IrExceptionTarget.MethodExit : null, IrType.Int64);
                        else Push(offset, new IrExpression.Binary(operation, left, right), Int32Operations.MayFault(operation) ? IrExceptionTarget.MethodExit : null);
                        break;
                    case "not":
                        int complement = Pop(offset);
                        Push(offset, valueTypes[complement] == IrType.Int64 ? new IrExpression.Not64(complement)
                            : new IrExpression.Unary(Int32UnaryOperation.BitwiseNot, complement), type: valueTypes[complement]); break;
                    case "neg" when integers64:
                        int negated = Pop(offset); var negatedType = valueTypes[negated];
                        Push(offset, negatedType == IrType.Int64 ? new IrExpression.Constant64(0) : new IrExpression.Constant(0), type: negatedType);
                        int zero = Pop(offset);
                        Push(offset, negatedType == IrType.Int64 ? new IrExpression.Binary64(Int64Operation.SubtractWrapping, zero, negated)
                            : new IrExpression.Binary(Int32Operation.SubtractWrapping, zero, negated), type: negatedType); break;
                    case "ceq" or "clt" or "clt.un" or "cgt" or "cgt.un":
                        int compareRight = Pop(offset), compareLeft = Pop(offset);
                        if (valueTypes[compareLeft] != valueTypes[compareRight]) Fail(DiagnosticIds.InconsistentStack, "Comparison operands require equal integer widths.", offset);
                        Push(offset, new IrExpression.Compare(opcode switch
                        {
                            "ceq" => Int32Comparison.Equal, "clt" => Int32Comparison.LessSigned, "clt.un" => Int32Comparison.LessUnsigned,
                            "cgt" => Int32Comparison.GreaterSigned, _ => Int32Comparison.GreaterUnsigned,
                        }, compareLeft, compareRight)); break;
                    case "call" when resolveCall is not null:
                        var callee = resolveCall(instruction);
                        var arguments = new int[callee.Parameters.Length];
                        for (int argument = arguments.Length - 1; argument >= 0; argument--) arguments[argument] = Pop(offset, callee.Parameters[argument]);
                        Push(offset, new IrExpression.Call(callee.Target, [.. arguments]), IrExceptionTarget.MethodExit, callee.ReturnType); break;
                    case "br" or "br.s": terminator = new IrJump(offset, Edge(instruction.Targets[0], offset)); break;
                    case "ret":
                        if (stack.Count != 1) Fail(DiagnosticIds.InconsistentStack, "Integer return requires exactly one value and no residual stack entries.", offset);
                        terminator = new IrReturn(offset, Pop(offset, ScalarType(input.Signature.ReturnType, integers64))); break;
                    default:
                        if (BranchComparison(opcode) is Int32Comparison comparison)
                        {
                            int? branchRight = comparison is Int32Comparison.Zero or Int32Comparison.Nonzero ? null : Pop(offset);
                            int branchLeft = Pop(offset);
                            if (branchRight is int compared && valueTypes[branchLeft] != valueTypes[compared])
                                Fail(DiagnosticIds.InconsistentStack, "Branch operands require equal integer widths.", offset);
                            terminator = new IrBranch(offset, comparison, branchLeft, branchRight,
                                Edge(instruction.Targets[0], offset), Edge(offset + instruction.Length, offset));
                        }
                        else Fail(DiagnosticIds.ExecutionUnsupported, "Instruction '" + opcode + "' is outside " + IrMethod.Profile + "; add a supported lowering before execution.", offset);
                        break;
                }
                if (stack.Count > input.MaxStack) Fail(DiagnosticIds.InconsistentStack, "Evaluation stack exceeds the declared maximum.", offset);
            }
            var last = input.Instructions[end - 1];
            terminator ??= new IrJump(last.Offset, Edge(last.Offset + last.Length, last.Offset));
            return new(id, blockOffset, incoming, [.. statements], terminator);

            void Add(IrStatement statement, int offset)
            {
                if (++_statements > IrLimits.MaximumStatements) Fail(DiagnosticIds.TranslationLimit, "Aggregate IR statement limit exceeded.", offset);
                statements.Add(statement);
            }
            void Push(int offset, IrExpression expression, IrExceptionTarget? exceptional = null, IrType? type = null)
            {
                type ??= IrType.Int32;
                int value = values++; valueTypes.Add(type); Add(new IrStatement.Define(new(value, type, offset, expression), exceptional), offset); stack.Add(value);
            }
            int Pop(int offset, IrType? expected = null)
            {
                if (stack.Count == 0) Fail(DiagnosticIds.InconsistentStack, "Instruction requires an integer stack operand.", offset);
                int value = stack[^1]; stack.RemoveAt(stack.Count - 1);
                if (expected is not null && valueTypes[value] != expected)
                    Fail(DiagnosticIds.InconsistentStack, "Operand width does not match the required integer type.", offset);
                return value;
            }
            IrEdge Edge(int targetOffset, int from)
            {
                if (!_blockAt.TryGetValue(targetOffset, out int target)) Fail(DiagnosticIds.InvalidControlTransfer, "Control flow falls outside this method.", from);
                ImmutableArray<IrType> transferred = [.. stack.Select(value => valueTypes[value])];
                if (_stacks[target] is { } expected && !expected.SequenceEqual(transferred))
                    Fail(DiagnosticIds.InconsistentStack, "Block IL_" + targetOffset.ToString("x4", System.Globalization.CultureInfo.InvariantCulture) +
                        " receives incompatible integer stack depths or widths.", from);
                if (_stacks[target] is null) { _stacks[target] = transferred; _pending.Enqueue(target); }
                _edgeArguments += stack.Count;
                if (_edgeArguments > IrLimits.MaximumEdgeArguments) Fail(DiagnosticIds.TranslationLimit, "Aggregate edge-argument limit exceeded.", from);
                return new(target, [.. stack]);
            }
        }

        private int Variable(MethodInstruction instruction, int count, string kind)
        {
            if (instruction.IntegerOperand is not int index || index < 0 || index >= count)
                Fail(DiagnosticIds.InvalidOperand, kind + " index is outside the selected method's signature.", instruction.Offset);
            return instruction.IntegerOperand!.Value;
        }
    }

    private static Int32Comparison? BranchComparison(string opcode) => CilControlFlow.BranchComparison(opcode);
}
