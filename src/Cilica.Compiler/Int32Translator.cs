using System;
using System.Collections.Generic;
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

    private sealed class Translation(MethodInput input, Func<MethodInstruction, (IrMethodId Target, int Arity)>? resolveCall = null)
    {
        private readonly Dictionary<int, int> _blockAt = [];
        private readonly Queue<int> _pending = new();
        private readonly List<(int Start, int End)> _ranges = [];
        private int?[] _depths = [];
        private int _statements, _inputs, _edgeArguments;

        private void Fail(string code, string message, int offset = 0) => throw new TranslationFailure(
            new(code, DiagnosticSeverity.Error, message, input.Source.Name + " token 0x" +
                input.Source.Token.ToString("x8", System.Globalization.CultureInfo.InvariantCulture) +
                " IL_" + offset.ToString("x4", System.Globalization.CultureInfo.InvariantCulture)));

        internal IrMethod Run()
        {
            bool Int32(TypeSignature type) => type is TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 };
            if ((input.Attributes & MethodAttributes.Static) == 0 || input.OwnerGenericArity != 0
                || input.Signature.GenericArity != 0 || input.Signature.Header != 0
                || !Int32(input.Signature.ReturnType) || input.Signature.Parameters.Any(p => !Int32(p))
                || input.Locals.Any(l => !Int32(l)))
                Fail(DiagnosticIds.ExecutionSignatureUnsupported, IrMethod.Profile +
                    " requires a static nongeneric default-convention method with Int32 parameters, locals and return type.");
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
            _depths = new int?[_ranges.Count]; _depths[0] = 0; _pending.Enqueue(0);
            var blocks = new IrBlock[_ranges.Count];
            while (_pending.TryDequeue(out int id)) blocks[id] = TranslateBlock(id, _depths[id]!.Value);
            for (int id = 0; id < blocks.Length; id++)
                if (blocks[id] is null) Fail(DiagnosticIds.ExecutionUnsupported,
                    "Unreachable blocks are outside this verified profile; remove dead IL or add unreachable-block analysis.", input.Instructions[_ranges[id].Start].Offset);
            return new(input.Source, [.. input.Signature.Parameters.Select(_ => IrType.Int32)], IrType.Int32,
                [.. blocks], [.. input.Locals.Select(_ => IrType.Int32)]);
        }

        private void Partition()
        {
            var indices = input.Instructions.Select((instruction, index) => (instruction.Offset, index)).ToDictionary(p => p.Offset, p => p.index);
            var leaders = new SortedSet<int> { 0 };
            for (int index = 0; index < input.Instructions.Length; index++)
            {
                MethodInstruction instruction = input.Instructions[index];
                bool branch = instruction.OpCode is "br" or "br.s" || BranchComparison(instruction.OpCode) is not null;
                if (branch)
                {
                    if (instruction.Targets.Length != 1 || !indices.TryGetValue(instruction.Targets[0], out int target))
                        Fail(DiagnosticIds.InvalidControlTransfer, "Branch target must be an instruction boundary inside this method.", instruction.Offset);
                    else leaders.Add(target);
                }
                if ((branch || instruction.OpCode == "ret") && index + 1 < input.Instructions.Length) leaders.Add(index + 1);
                if (leaders.Count > IrLimits.MaximumBlocks) Fail(DiagnosticIds.TranslationLimit, "Basic-block limit exceeded.", instruction.Offset);
            }
            int[] starts = [.. leaders];
            for (int id = 0; id < starts.Length; id++)
            {
                _ranges.Add((starts[id], id + 1 < starts.Length ? starts[id + 1] : input.Instructions.Length));
                _blockAt.Add(input.Instructions[starts[id]].Offset, id);
            }
        }

        private IrBlock TranslateBlock(int id, int depth)
        {
            var (start, end) = _ranges[id]; int blockOffset = input.Instructions[start].Offset;
            if (depth > input.MaxStack) Fail(DiagnosticIds.InconsistentStack, "Incoming stack exceeds the declared maximum.", blockOffset);
            _inputs += depth;
            if (_inputs > IrLimits.MaximumInputs) Fail(DiagnosticIds.TranslationLimit, "Aggregate block-input limit exceeded.", blockOffset);
            var statements = new List<IrStatement>(); var stack = new List<int>(); int values = 0;
            IrTerminator? terminator = null;
            for (int index = 0; index < depth; index++) Push(blockOffset, new IrExpression.StackInput(index));
            for (int index = start; index < end; index++)
            {
                var instruction = input.Instructions[index]; int offset = instruction.Offset;
                string opcode = instruction.OpCode;
                switch (opcode)
                {
                    case "nop": break;
                    case "ldarg.0" or "ldarg.1" or "ldarg.2" or "ldarg.3" or "ldarg.s" or "ldarg":
                        Push(offset, new IrExpression.Parameter(Variable(instruction, input.Signature.Parameters.Count, "Argument"))); break;
                    case "ldloc.0" or "ldloc.1" or "ldloc.2" or "ldloc.3" or "ldloc.s" or "ldloc":
                        Push(offset, new IrExpression.Local(Variable(instruction, input.Locals.Length, "Local"))); break;
                    case "stloc.0" or "stloc.1" or "stloc.2" or "stloc.3" or "stloc.s" or "stloc":
                        int local = Variable(instruction, input.Locals.Length, "Local");
                        Add(new IrStatement.StoreLocal(offset, local, Pop(offset)), offset); break;
                    case "ldc.i4.m1" or "ldc.i4.0" or "ldc.i4.1" or "ldc.i4.2" or "ldc.i4.3" or "ldc.i4.4" or "ldc.i4.5" or "ldc.i4.6" or "ldc.i4.7" or "ldc.i4.8":
                        Push(offset, new IrExpression.Constant(opcode == "ldc.i4.m1" ? -1 : opcode[^1] - '0')); break;
                    case "ldc.i4.s" or "ldc.i4": Push(offset, new IrExpression.Constant(instruction.IntegerOperand!.Value)); break;
                    case "dup": int duplicated = Pop(offset); stack.Add(duplicated); stack.Add(duplicated); break;
                    case "pop": Pop(offset); break;
                    case "add" or "sub" or "mul" or "and" or "or" or "xor" or "shl" or "shr" or "shr.un" or "div" or "div.un"
                        or "add.ovf" or "sub.ovf" or "mul.ovf" or "add.ovf.un" or "sub.ovf.un" or "mul.ovf.un":
                        int right = Pop(offset), left = Pop(offset);
                        Int32Operation operation = opcode switch
                        {
                            "add" => Int32Operation.AddWrapping, "sub" => Int32Operation.SubtractWrapping, "mul" => Int32Operation.MultiplyWrapping,
                            "and" => Int32Operation.BitwiseAnd, "or" => Int32Operation.BitwiseOr, "xor" => Int32Operation.BitwiseXor,
                            "shl" => Int32Operation.ShiftLeft, "shr" => Int32Operation.ShiftRightSigned, "shr.un" => Int32Operation.ShiftRightUnsigned,
                            "div" => Int32Operation.DivideSigned, "div.un" => Int32Operation.DivideUnsigned,
                            "add.ovf" => Int32Operation.AddCheckedSigned, "sub.ovf" => Int32Operation.SubtractCheckedSigned,
                            "mul.ovf" => Int32Operation.MultiplyCheckedSigned, "add.ovf.un" => Int32Operation.AddCheckedUnsigned,
                            "sub.ovf.un" => Int32Operation.SubtractCheckedUnsigned, "mul.ovf.un" => Int32Operation.MultiplyCheckedUnsigned,
                            _ => throw new InvalidOperationException("Supported binary instruction has no lowering."),
                        };
                        Push(offset, new IrExpression.Binary(operation, left, right), Int32Operations.MayFault(operation) ? IrExceptionTarget.MethodExit : null); break;
                    case "not": Push(offset, new IrExpression.Unary(Int32UnaryOperation.BitwiseNot, Pop(offset))); break;
                    case "ceq" or "clt" or "clt.un" or "cgt" or "cgt.un":
                        int compareRight = Pop(offset), compareLeft = Pop(offset);
                        Push(offset, new IrExpression.Compare(opcode switch
                        {
                            "ceq" => Int32Comparison.Equal, "clt" => Int32Comparison.LessSigned, "clt.un" => Int32Comparison.LessUnsigned,
                            "cgt" => Int32Comparison.GreaterSigned, _ => Int32Comparison.GreaterUnsigned,
                        }, compareLeft, compareRight)); break;
                    case "call" when resolveCall is not null:
                        var callee = resolveCall(instruction);
                        var arguments = new int[callee.Arity];
                        for (int argument = arguments.Length - 1; argument >= 0; argument--) arguments[argument] = Pop(offset);
                        Push(offset, new IrExpression.Call(callee.Target, [.. arguments]), IrExceptionTarget.MethodExit); break;
                    case "br" or "br.s": terminator = new IrJump(offset, Edge(instruction.Targets[0], offset)); break;
                    case "ret":
                        if (stack.Count != 1) Fail(DiagnosticIds.InconsistentStack, "Int32 return requires exactly one value and no residual stack entries.", offset);
                        terminator = new IrReturn(offset, Pop(offset)); break;
                    default:
                        if (BranchComparison(opcode) is Int32Comparison comparison)
                        {
                            int? branchRight = comparison is Int32Comparison.Zero or Int32Comparison.Nonzero ? null : Pop(offset);
                            int branchLeft = Pop(offset);
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
            return new(id, blockOffset, [.. Enumerable.Repeat(IrType.Int32, depth)], [.. statements], terminator);

            void Add(IrStatement statement, int offset)
            {
                if (++_statements > IrLimits.MaximumStatements) Fail(DiagnosticIds.TranslationLimit, "Aggregate IR statement limit exceeded.", offset);
                statements.Add(statement);
            }
            void Push(int offset, IrExpression expression, IrExceptionTarget? exceptional = null)
            {
                int value = values++; Add(new IrStatement.Define(new(value, IrType.Int32, offset, expression), exceptional), offset); stack.Add(value);
            }
            int Pop(int offset)
            {
                if (stack.Count == 0) Fail(DiagnosticIds.InconsistentStack, "Instruction requires an Int32 stack operand.", offset);
                int value = stack[^1]; stack.RemoveAt(stack.Count - 1); return value;
            }
            IrEdge Edge(int targetOffset, int from)
            {
                if (!_blockAt.TryGetValue(targetOffset, out int target)) Fail(DiagnosticIds.InvalidControlTransfer, "Control flow falls outside this method.", from);
                if (_depths[target] is int expected && expected != stack.Count)
                    Fail(DiagnosticIds.InconsistentStack, "Block IL_" + targetOffset.ToString("x4", System.Globalization.CultureInfo.InvariantCulture) +
                        " receives incompatible Int32 stack depths (" + expected.ToString(System.Globalization.CultureInfo.InvariantCulture) + " and " + stack.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + ").", from);
                if (_depths[target] is null) { _depths[target] = stack.Count; _pending.Enqueue(target); }
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

    private static Int32Comparison? BranchComparison(string opcode) => (opcode.EndsWith(".s", StringComparison.Ordinal) ? opcode[..^2] : opcode) switch
    {
        "brfalse" => Int32Comparison.Zero, "brtrue" => Int32Comparison.Nonzero,
        "beq" => Int32Comparison.Equal, "bne.un" => Int32Comparison.NotEqual,
        "blt" => Int32Comparison.LessSigned, "bgt" => Int32Comparison.GreaterSigned,
        "ble" => Int32Comparison.LessOrEqualSigned, "bge" => Int32Comparison.GreaterOrEqualSigned,
        "blt.un" => Int32Comparison.LessUnsigned, "bgt.un" => Int32Comparison.GreaterUnsigned,
        "ble.un" => Int32Comparison.LessOrEqualUnsigned, "bge.un" => Int32Comparison.GreaterOrEqualUnsigned,
        _ => null,
    };
}
