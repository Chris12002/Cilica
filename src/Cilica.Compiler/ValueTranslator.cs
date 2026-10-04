using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

public sealed record ValueTranslationResult(ManagedLocationIrProgram? Program,
    ImmutableArray<ObjectMethodBinding> Bindings, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Program is not null;
}

public sealed record ValueArrayTranslationResult(ValueArrayIrProgram? Program, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Program is not null;
}

/// <summary>Translates bounded static CIL value operations into the shared copy/borrow IR.</summary>
public static class ValueTranslator
{
    public const string Profile = "value-cil-v1";
    public const string ArrayProfile = "value-arrays-cil-v1";

    public static ValueArrayTranslationResult TranslateArrays(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits = null)
    {
        var result = TranslateCore(resolver, assembly, entry, runtimeLibrary, limits, arrays: true);
        return new(result.Program is null ? null : new(result.Program, result.Bindings), result.Diagnostics);
    }

    public static ValueTranslationResult Translate(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits = null) =>
        TranslateCore(resolver, assembly, entry, runtimeLibrary, limits, arrays: false);

    private static ValueTranslationResult TranslateCore(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits, bool arrays)
    {
        var discovered = arrays ? ValueProgramDiscovery.ReadArrays(resolver, assembly, entry, runtimeLibrary, limits)
            : ValueProgramDiscovery.Read(resolver, assembly, entry, runtimeLibrary, limits);
        if (discovered.Input is not { } input) return new(null, [], discovered.Diagnostics);
        try
        {
            var methods = input.Methods.ToDictionary(m => m.Binding.Method);
            var fields = input.Types.Types.SelectMany(t => t.Fields).ToDictionary(f => f.Id);
            var resources = new ProgramResources(); var translated = new List<IrMethod>();
            foreach (var method in input.Methods)
            {
                var body = new Body(method, methods, fields, arrays,
                    input.Types.References.Arrays.FirstOrDefault(a => a.ArrayElement!.Kind == ObjectArrayElementKind.Int32)).Translate();
                if (!resources.Add(body)) Fail(method, 0, "Aggregate value IR resource limit exceeded.", DiagnosticIds.TranslationLimit);
                translated.Add(body);
            }
            var program = new ManagedLocationIrProgram(new(input.Entry, [.. translated], input.Types));
            ImmutableArray<ObjectMethodBinding> bindings = [.. input.Methods.Select(m => m.Binding)];
            try
            {
                if (arrays) _ = ValueRootAssignment.Assign(new ValueArrayIrProgram(program, bindings));
                else _ = ValueRootAssignment.Assign(program);
            }
            catch (ArgumentException error) { Fail(methods[input.Entry], 0, error.Message); }
            return new(program, bindings, []);
        }
        catch (Failure failure) { return new(null, [], [failure.Diagnostic]); }
    }

    private sealed class Failure(Diagnostic diagnostic) : Exception(diagnostic.Message)
    {
        internal Diagnostic Diagnostic { get; } = diagnostic;
    }
    private static void Fail(ValueMethodInput method, int offset, string message, string code = DiagnosticIds.ExecutionUnsupported) =>
        throw new Failure(new(code, DiagnosticSeverity.Error, message,
            method.Input.Source.Name + " token 0x" + method.Input.Source.Token.ToString("x8", CultureInfo.InvariantCulture)
            + " IL_" + offset.ToString("x4", CultureInfo.InvariantCulture)));

    private sealed class Body(ValueMethodInput method, Dictionary<IrMethodId, ValueMethodInput> methods,
        Dictionary<ObjectFieldId, ObjectField> fields, bool arrays, ObjectTypeId? integerArray)
    {
        private int _statements, _inputs, _edges;
        private void Error(int offset, string message, string code = DiagnosticIds.InconsistentStack) => Fail(method, offset, message, code);
        internal IrMethod Translate()
        {
            var input = method.Input;
            if (input.Instructions.IsEmpty) Error(0, "Method body is empty.");
            var partition = CilControlFlow.Partition(input, (code, message, offset) => Error(offset, message, code));
            var incoming = new ImmutableArray<IrType>[partition.Ranges.Count];
            var blocks = new IrBlock[partition.Ranges.Count]; var pending = new Queue<int>();
            incoming[0] = []; pending.Enqueue(0);
            while (pending.TryDequeue(out int id)) blocks[id] = Block(id);
            for (int id = 0; id < blocks.Length; id++)
                if (blocks[id] is null) Error(input.Instructions[partition.Ranges[id].Start].Offset,
                    "Unreachable blocks require later analysis; remove dead IL before value translation.", DiagnosticIds.ExecutionUnsupported);
            return new(input.Source, method.Parameters, method.ReturnType, [.. blocks], method.Locals);

            IrBlock Block(int id)
            {
                var (start, end) = partition.Ranges[id]; int blockOffset = input.Instructions[start].Offset;
                _inputs += incoming[id].Length;
                if (_inputs > IrLimits.MaximumInputs) Error(blockOffset, "Aggregate block-input limit exceeded.", DiagnosticIds.TranslationLimit);
                var statements = new List<IrStatement>(); var values = new List<IrType>(); var stack = new List<int>();
                IrTerminator? terminator = null;
                for (int i = 0; i < incoming[id].Length; i++) Push(blockOffset, incoming[id][i], new IrExpression.StackInput(i));
                for (int index = start; index < end; index++)
                {
                    var instruction = input.Instructions[index]; int offset = instruction.Offset; string opcode = instruction.OpCode;
                    if (Int32Operations.FromOpcode(opcode) is { } operation)
                    {
                        int right = Pop(offset, IrType.Int32), left = Pop(offset, IrType.Int32);
                        Push(offset, IrType.Int32, new IrExpression.Binary(operation, left, right), Int32Operations.MayFault(operation));
                    }
                    else switch (opcode)
                    {
                        case "nop": break;
                        case "newarr" when arrays:
                            var arrayType = method.ArrayOperands[offset];
                            Push(offset, IrType.Reference(arrayType), new IrExpression.NewArray(arrayType, Pop(offset, IrType.Int32)), true); break;
                        case "ldlen" when arrays:
                            if (index + 1 >= end || input.Instructions[index + 1].OpCode != "conv.i4")
                                Error(offset, "Array length requires an adjacent conv.i4 in the same block; native integer flow requires later lowering.", DiagnosticIds.ExecutionUnsupported);
                            int lengthArray = PopArray(offset); ++index;
                            Push(offset, IrType.Int32, new IrExpression.ArrayLength(lengthArray), true); break;
                        case "ldelem.i4" when arrays:
                            int loadIndex = Pop(offset, IrType.Int32), loadArray = PopArray(offset);
                            Push(offset, IrType.Int32, new IrExpression.LoadElement(integerArray!, loadArray, loadIndex), true); break;
                        case "stelem.i4" when arrays:
                            int elementValue = Pop(offset, IrType.Int32), storeIndex = Pop(offset, IrType.Int32), storeArray = PopArray(offset);
                            Add(new IrStatement.StoreElement(offset, integerArray!, storeArray, storeIndex, elementValue), offset); break;
                        case "ldnull": Push(offset, IrType.NullReference, new IrExpression.Null()); break;
                        case "ldarg.0" or "ldarg.1" or "ldarg.2" or "ldarg.3" or "ldarg.s" or "ldarg":
                            int argument = Variable(instruction, method.Parameters.Length);
                            Push(offset, method.Parameters[argument], new IrExpression.Parameter(argument)); break;
                        case "ldloc.0" or "ldloc.1" or "ldloc.2" or "ldloc.3" or "ldloc.s" or "ldloc":
                            int local = Variable(instruction, method.Locals.Length);
                            Push(offset, method.Locals[local], new IrExpression.Local(local)); break;
                        case "ldloca" or "ldloca.s":
                            int localAddress = Variable(instruction, method.Locals.Length);
                            Push(offset, IrType.Location(method.Locals[localAddress]), new IrExpression.AddressLocal(localAddress)); break;
                        case "ldarga" or "ldarga.s":
                            int parameterAddress = Variable(instruction, method.Parameters.Length);
                            if (method.Parameters[parameterAddress].Kind == IrTypeKind.ManagedLocation)
                                Error(offset, "Taking the address of a borrowed parameter would create a nested byref.", DiagnosticIds.ExecutionUnsupported);
                            Push(offset, IrType.Location(method.Parameters[parameterAddress]), new IrExpression.AddressParameter(parameterAddress)); break;
                        case "stloc.0" or "stloc.1" or "stloc.2" or "stloc.3" or "stloc.s" or "stloc":
                            int storedLocal = Variable(instruction, method.Locals.Length);
                            Add(new IrStatement.StoreLocal(offset, storedLocal, Pop(offset, method.Locals[storedLocal])), offset); break;
                        case "ldc.i4.m1" or "ldc.i4.0" or "ldc.i4.1" or "ldc.i4.2" or "ldc.i4.3" or "ldc.i4.4" or "ldc.i4.5" or "ldc.i4.6" or "ldc.i4.7" or "ldc.i4.8":
                            Push(offset, IrType.Int32, new IrExpression.Constant(opcode == "ldc.i4.m1" ? -1 : opcode[^1] - '0')); break;
                        case "ldc.i4.s" or "ldc.i4": Push(offset, IrType.Int32, new IrExpression.Constant(instruction.IntegerOperand!.Value)); break;
                        case "dup": int duplicate = Pop(offset); stack.Add(duplicate); stack.Add(duplicate); break;
                        case "pop": Pop(offset); break;
                        case "not": Push(offset, IrType.Int32, new IrExpression.Unary(Int32UnaryOperation.BitwiseNot, Pop(offset, IrType.Int32))); break;
                        case "neg":
                            int negated = Pop(offset, IrType.Int32), zero = Define(offset, IrType.Int32, new IrExpression.Constant(0));
                            Push(offset, IrType.Int32, new IrExpression.Binary(Int32Operation.SubtractWrapping, zero, negated)); break;
                        case "conv.i4": int unchanged = Pop(offset, IrType.Int32); stack.Add(unchanged); break;
                        case "ceq" or "clt" or "clt.un" or "cgt" or "cgt.un":
                            int comparedRight = Pop(offset), comparedLeft = Pop(offset);
                            var compared = opcode switch
                            {
                                "ceq" => Int32Comparison.Equal, "clt" => Int32Comparison.LessSigned, "clt.un" => Int32Comparison.LessUnsigned,
                                "cgt" => Int32Comparison.GreaterSigned, _ => Int32Comparison.GreaterUnsigned,
                            };
                            if (arrays && opcode == "cgt.un" && Reference(values[comparedLeft]) && values[comparedRight] == IrType.NullReference)
                                compared = Int32Comparison.NotEqual;
                            Comparison(compared, comparedLeft, comparedRight, offset);
                            Push(offset, IrType.Int32, new IrExpression.Compare(compared, comparedLeft, comparedRight)); break;
                        case "call":
                            var target = methods[method.Calls[offset]]; var arguments = new int[target.Parameters.Length];
                            for (int i = arguments.Length - 1; i >= 0; i--) arguments[i] = Pop(offset, target.Parameters[i]);
                            if (target.ReturnType == IrType.Void) Add(new IrStatement.Invoke(offset, target.Binding.Method, [.. arguments]), offset);
                            else Push(offset, target.ReturnType, new IrExpression.Call(target.Binding.Method, [.. arguments]), true);
                            break;
                        case "ldfld" or "ldflda" or "stfld":
                            var field = fields[method.Fields[offset]]; var fieldType = Storage(field.Storage);
                            int fieldValue = opcode == "stfld" ? Pop(offset, fieldType) : -1;
                            int receiver = Pop(offset); var receiverType = values[receiver];
                            if (opcode == "ldfld" && receiverType == IrType.Value(field.Id.Owner))
                                Push(offset, fieldType, new IrExpression.ValueField(field.Id, receiver));
                            else
                            {
                                if (receiverType.Kind != IrTypeKind.ManagedLocation || receiverType.Element != IrType.Value(field.Id.Owner))
                                    Error(offset, "Value field operation requires the exact inline receiver or its managed location.");
                                var locationType = IrType.Location(fieldType, receiverType.ReadOnly || (field.Attributes & FieldAttributes.InitOnly) != 0);
                                int address = Define(offset, locationType, new IrExpression.LocationField(field.Id, receiver));
                                if (opcode == "ldflda") stack.Add(address);
                                else if (opcode == "ldfld") Push(offset, fieldType, new IrExpression.LoadLocation(address), true);
                                else { Writable(address, offset); Add(new IrStatement.StoreLocation(offset, address, fieldValue), offset); }
                            }
                            break;
                        case "initobj" or "ldobj" or "stobj" or "cpobj":
                            var valueType = IrType.Value(method.TypeOperands[offset]);
                            int stored = opcode == "stobj" ? Pop(offset, valueType) : -1;
                            if (opcode == "cpobj")
                            {
                                int source = Location(offset, valueType);
                                // Snapshot every leaf before touching a possibly overlapping destination.
                                stored = Define(offset, valueType, new IrExpression.LoadLocation(source), true);
                            }
                            int destination = Location(offset, valueType);
                            if (opcode == "ldobj") Push(offset, valueType, new IrExpression.LoadLocation(destination), true);
                            else
                            {
                                Writable(destination, offset);
                                Add(opcode == "initobj" ? new IrStatement.InitializeLocation(offset, destination)
                                    : new IrStatement.StoreLocation(offset, destination, stored), offset);
                            }
                            break;
                        case "ldind.i4" or "ldind.ref":
                            int read = Location(offset, opcode == "ldind.i4" ? IrType.Int32 : null);
                            if (opcode == "ldind.ref" && values[read].Element!.Kind != IrTypeKind.Reference) Error(offset, "ldind.ref requires reference storage.");
                            Push(offset, values[read].Element!, new IrExpression.LoadLocation(read), true); break;
                        case "stind.i4" or "stind.ref":
                            int writeValue = Pop(offset), write = Location(offset, opcode == "stind.i4" ? IrType.Int32 : null);
                            if (opcode == "stind.ref" && values[write].Element!.Kind != IrTypeKind.Reference) Error(offset, "stind.ref requires reference storage.");
                            Compatible(values[writeValue], values[write].Element!, offset); Writable(write, offset);
                            Add(new IrStatement.StoreLocation(offset, write, writeValue), offset); break;
                        case "br" or "br.s": terminator = new IrJump(offset, Edge(instruction.Targets[0], offset)); break;
                        case "ret":
                            int returned = method.ReturnType == IrType.Void ? -1 : Pop(offset, method.ReturnType);
                            if (stack.Count != 0) Error(offset, "Return leaves residual evaluation-stack values.");
                            terminator = returned < 0 ? new IrVoidReturn(offset) : new IrReturn(offset, returned); break;
                        default:
                            if (CilControlFlow.BranchComparison(opcode) is { } comparison)
                            {
                                int? right = comparison is Int32Comparison.Zero or Int32Comparison.Nonzero ? null : Pop(offset);
                                int left = Pop(offset);
                                if (right is { } comparedRightId) Comparison(comparison, left, comparedRightId, offset);
                                else if (values[left] != IrType.Int32 && !(arrays && Reference(values[left]))) Error(offset, "Branch requires Int32 or reference truth value.");
                                terminator = new IrBranch(offset, comparison, left, right, Edge(instruction.Targets[0], offset), Edge(offset + instruction.Length, offset));
                            }
                            else Error(offset, "Instruction '" + opcode + "' is outside " + (arrays ? ArrayProfile : Profile) + "; add a supported lowering before execution.", DiagnosticIds.ExecutionUnsupported);
                            break;
                    }
                    if (stack.Count > input.MaxStack) Error(offset, "Evaluation stack exceeds the declared maximum.");
                }
                var last = input.Instructions[end - 1];
                terminator ??= new IrJump(last.Offset, Edge(last.Offset + last.Length, last.Offset));
                return new(id, blockOffset, incoming[id], [.. statements], terminator);

                void Add(IrStatement statement, int offset)
                {
                    if (++_statements > IrLimits.MaximumStatements) Error(offset, "Aggregate IR statement limit exceeded.", DiagnosticIds.TranslationLimit);
                    statements.Add(statement);
                }
                int Define(int offset, IrType type, IrExpression expression, bool fault = false)
                {
                    int id = values.Count; values.Add(type);
                    Add(new IrStatement.Define(new(id, type, offset, expression), fault ? IrExceptionTarget.MethodExit : null), offset); return id;
                }
                void Push(int offset, IrType type, IrExpression expression, bool fault = false) => stack.Add(Define(offset, type, expression, fault));
                int Pop(int offset, IrType? expected = null)
                {
                    if (stack.Count == 0) Error(offset, "Instruction requires an evaluation-stack operand.");
                    int id = stack[^1]; stack.RemoveAt(stack.Count - 1);
                    if (expected is not null) Compatible(values[id], expected, offset); return id;
                }
                int Location(int offset, IrType? element)
                {
                    int id = Pop(offset);
                    if (values[id].Kind != IrTypeKind.ManagedLocation || element is not null && values[id].Element != element)
                        Error(offset, "Instruction requires a managed location with the exact element type.");
                    return id;
                }
                void Writable(int id, int offset)
                {
                    if (values[id].ReadOnly) Error(offset, "Readonly managed storage cannot be mutated.");
                }
                int PopArray(int offset)
                {
                    int array = Pop(offset);
                    if (integerArray is null || values[array] != IrType.NullReference && values[array] != IrType.Reference(integerArray))
                        Error(offset, "Array operation requires an Int32 vector or null.");
                    return array;
                }
                void Comparison(Int32Comparison comparison, int left, int right, int offset)
                {
                    if (values[left] == IrType.Int32 && values[right] == IrType.Int32) return;
                    if (arrays && comparison is (Int32Comparison.Equal or Int32Comparison.NotEqual) && Reference(values[left]) && Reference(values[right])) return;
                    Error(offset, "Comparison requires Int32 operands or supported reference identity equality.");
                }
                IrEdge Edge(int offset, int from)
                {
                    if (!partition.BlockAt.TryGetValue(offset, out int target)) Error(from, "Control transfer falls outside this method.", DiagnosticIds.InvalidControlTransfer);
                    var types = stack.Select(id => values[id]).ToImmutableArray();
                    if (types.Any(t => t.Kind == IrTypeKind.ManagedLocation)) Error(from, "Managed locations cannot cross block joins in this profile.", DiagnosticIds.ExecutionUnsupported);
                    if (!incoming[target].IsDefault && !incoming[target].SequenceEqual(types)) Error(from, "Block receives incompatible stack types or depths; reference widening requires later lowering.");
                    if (incoming[target].IsDefault) { incoming[target] = types; pending.Enqueue(target); }
                    _edges += types.Length;
                    if (_edges > IrLimits.MaximumEdgeArguments) Error(from, "Aggregate edge-argument limit exceeded.", DiagnosticIds.TranslationLimit);
                    return new(target, [.. stack]);
                }
            }
        }
        private void Compatible(IrType actual, IrType expected, int offset)
        {
            if (actual != expected && !(actual == IrType.NullReference && expected.Kind == IrTypeKind.Reference))
                Error(offset, "Stack operand does not match its required exact storage type.");
        }
        private static bool Reference(IrType type) => type.Kind is IrTypeKind.Reference or IrTypeKind.NullReference;
        private int Variable(MethodInstruction instruction, int count)
        {
            if (instruction.IntegerOperand is not int index || index < 0 || index >= count)
                Error(instruction.Offset, "Variable index is outside the method signature.", DiagnosticIds.InvalidOperand);
            return instruction.IntegerOperand!.Value;
        }
        private static IrType Storage(ObjectStorage storage) => storage switch
        {
            ObjectStorage.Int32 => IrType.Int32,
            ObjectStorage.Value value => IrType.Value(value.Type),
            ObjectStorage.Reference reference => IrType.Reference(reference.Type),
            _ => throw new InvalidOperationException("Unknown validated storage."),
        };
    }
}
