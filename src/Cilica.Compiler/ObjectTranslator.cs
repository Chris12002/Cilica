using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

/// <summary>Bounded typed object-operation translation; inspected assemblies are never executed.</summary>
public static class ObjectTranslator
{
    public const int MaximumInferenceWork = 262144;
    public static ObjectTranslationResult Translate(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, ResolvedAssembly runtimeLibrary, MethodReadLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        try { return new(new Translation(resolver, runtimeLibrary, limits ?? new()).Run(assembly, entry), []); }
        catch (Failure failure) { return new(null, [failure.Diagnostic]); }
    }

    private sealed class Failure(Diagnostic diagnostic) : Exception(diagnostic.Message)
    {
        internal Diagnostic Diagnostic { get; } = diagnostic;
    }

    private sealed class Translation(AssemblyResolver resolver, ResolvedAssembly runtimeLibrary, MethodReadLimits limits)
    {
        private sealed record Work(ResolvedMethod Resolved, MethodInput Input, ObjectMethodBinding Binding,
            ImmutableArray<IrType> Parameters, IrType Return, ImmutableArray<IrType> Locals, Work? Parent, int Offset);
        private readonly Dictionary<IrMethodId, Work> _methods = [];
        private readonly Dictionary<ObjectTypeId, ResolvedType> _roots = [];
        private readonly Dictionary<ObjectTypeId, TypeInput> _snapshots = [];
        private readonly Dictionary<ObjectTypeId, Dictionary<int, FieldInput>> _snapshotFields = [];
        private readonly Queue<Work> _pending = new();
        private readonly List<IrMethod> _translated = [];
        private readonly ProgramResources _resources = new();
        private ObjectTypeId _object = null!;
        private int _bytes, _instructions, _arguments, _inferenceWork;
        private readonly Dictionary<(IrMethodId Method, int Offset), Work> _callSites = [];
        private readonly MemberResolutionBudget _metadata = new(ObjectModelLimits.MaximumMetadataMembers);
        private readonly Dictionary<(string Hash, int Token), ResolvedMethod> _callTargets = [];
        private readonly Dictionary<(string Hash, int Token), ResolvedField> _fieldTargets = [];
        private readonly Dictionary<(string Hash, int Token), ObjectTypeId> _typeTargets = [];

        internal ObjectIrProgram Run(ResolvedAssembly assembly, MethodDefinitionHandle entry)
        {
            var selected = resolver.ResolveRuntimeType(runtimeLibrary, PrimitiveTypeCode.Object);
            if (selected.Value is not ResolvedType objectBase) throw new Failure(selected.Diagnostics[0]);
            _object = Include(objectBase); // Explicit selected library, never host runtime probing.
            var declared = resolver.ResolveDeclaredMethod(assembly, entry, _metadata);
            if (declared.Value is not ResolvedMethod resolved) throw new Failure(declared.Diagnostics[0]);
            Work root = Discover(resolved, null, 0);
            if (root.Binding.Kind != ObjectMethodKind.Static || root.Return != IrType.Int32 || root.Parameters.Any(p => p != IrType.Int32))
                Fail(root, "Entry requires a static Int32 signature; object references stay internal.", 0);
            while (_pending.TryDequeue(out Work? work))
            {
                var method = Body(work);
                if (!_resources.Add(method)) Fail(work, "Aggregate program IR limit exceeded.", 0, DiagnosticIds.TranslationLimit);
                _translated.Add(method);
            }
            var catalog = ObjectModelBuilder.BuildCatalog(resolver, [.. _roots.Values], runtimeLibrary, ReadType);
            if (catalog.Catalog is null) throw new Failure(catalog.Diagnostics[0]);
            var program = new ObjectIrProgram(new(IrMethodId.From(root.Input.Source),
                [.. _translated.OrderBy(m => m.Source.ContentHash, StringComparer.Ordinal).ThenBy(m => m.Source.Token)], catalog.Catalog),
                [.. _methods.Values.Select(w => w.Binding).OrderBy(b => b.Method.ContentHash, StringComparer.Ordinal).ThenBy(b => b.Method.Token)]);
            try { ObjectIrValidator.Validate(program); }
            catch (ArgumentException exception) { Fail(root, exception.Message, 0); }
            return program;
        }

        private ObjectTypeId Include(ResolvedType type)
        {
            var id = ObjectModelBuilder.Id(type);
            if (!_roots.ContainsKey(id) && _roots.Count >= ObjectModelLimits.MaximumTypes)
                throw new Failure(new(DiagnosticIds.TranslationLimit, DiagnosticSeverity.Error, "Object signature/layout root limit exceeded."));
            _roots.TryAdd(id, type); return id;
        }

        private TypeInput ReadType(ResolvedType type, TypeReadLimits budget)
        {
            var id = ObjectModelBuilder.Id(type);
            if (_snapshots.TryGetValue(id, out var known)) return known;
            int remaining = _metadata.Remaining;
            if (remaining <= 0) throw new Failure(new(DiagnosticIds.TranslationLimit, DiagnosticSeverity.Error, "Aggregate object metadata-member limit exceeded."));
            var read = resolver.ReadTypeInput(type.Assembly, type.Handle, budget with { MaximumMembers = Math.Min(budget.MaximumMembers, remaining) });
            if (read.Value is not TypeInput input) throw new Failure(read.Diagnostics[0]);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) throw new Failure(new(DiagnosticIds.TranslationLimit, DiagnosticSeverity.Error, "Aggregate metadata budget exceeded."));
            _snapshots.Add(id, input); _snapshotFields.Add(id, input.Fields.ToDictionary(f => f.Token)); return input;
        }

        private Work Discover(ResolvedMethod resolved, Work? parent, int offset)
        {
            var owner = resolved.DeclaringType.Definition;
            var key = new IrMethodId(owner.Assembly.ContentHash.ToLowerInvariant(), MetadataTokens.GetToken(resolved.Handle));
            if (_methods.TryGetValue(key, out var known)) return known;
            if (_methods.Count >= ProgramLimits.MaximumMethods || _bytes >= ProgramLimits.MaximumBodyBytes
                || _instructions >= ProgramLimits.MaximumInstructions || _metadata.Remaining == 0)
                Fail(parent, "Aggregate method, IL or metadata-member limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            var read = resolver.ReadMethodInput(owner.Assembly, resolved.Handle, limits with
            {
                MaximumBodyBytes = Math.Min(limits.MaximumBodyBytes, ProgramLimits.MaximumBodyBytes - _bytes),
                MaximumInstructions = Math.Min(limits.MaximumInstructions, ProgramLimits.MaximumInstructions - _instructions),
                MaximumInitializerMembers = Math.Min(limits.MaximumInitializerMembers, _metadata.Remaining),
            });
            if (read.Value is not MethodInput input) throw new Failure(read.Diagnostics[0] with { Message = read.Diagnostics[0].Message + Path(parent, offset) });
            _bytes += input.CodeSize; _instructions += input.Instructions.Length;
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(parent, "Aggregate metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
            if (input.IsReferenceAssembly || input.OwnerGenericArity != 0 || input.Signature.GenericArity != 0
                || resolved.DeclaringType.Arguments.Count != 0 || resolved.MethodArguments.Count != 0
                || input.Signature.Header is not (0 or 0x20) || input.HasTypeInitializer || input.HasModuleInitializer
                || input.ExceptionRegionCount != 0 || !input.Locals.IsEmpty && !input.InitializeLocals
                || (input.Implementation & (MethodImplAttributes.CodeTypeMask | MethodImplAttributes.ManagedMask | MethodImplAttributes.InternalCall | MethodImplAttributes.Synchronized)) != MethodImplAttributes.IL
                || (input.Attributes & ~(MethodAttributes.MemberAccessMask | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)) != 0
                || (input.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.PrivateScope)
                Fail(parent, "Object operations require nongeneric ordinary managed IL without virtual dispatch, initialization, exception regions or uninitialized locals.", offset);
            var ownerId = ObjectModelBuilder.Id(owner);
            bool constructor = input.DeclaredName == ".ctor";
            bool isStatic = (input.Attributes & MethodAttributes.Static) != 0;
            if (constructor && (isStatic || (input.Attributes & (MethodAttributes.SpecialName | MethodAttributes.RTSpecialName))
                != (MethodAttributes.SpecialName | MethodAttributes.RTSpecialName))) Fail(parent, "Invalid constructor declaration flags.", offset);
            var kind = constructor ? ownerId == _object ? ObjectMethodKind.ObjectBaseConstructor : ObjectMethodKind.Constructor
                : isStatic ? ObjectMethodKind.Static : ObjectMethodKind.Instance;
            if (!isStatic) Include(owner);
            IrType Type(TypeSignature signature, bool allowVoid = false) => signature switch
            {
                TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 } => IrType.Int32,
                TypeSignature.Primitive { Code: PrimitiveTypeCode.Void } when allowVoid => IrType.Void,
                TypeSignature.Primitive { Code: PrimitiveTypeCode.Object } => IrType.Reference(_object),
                TypeSignature.Named { Kind: SignatureTypeKind.Class } named => IrType.Reference(Include(named.Definition)),
                _ => throw new Failure(new(DiagnosticIds.ExecutionSignatureUnsupported, DiagnosticSeverity.Error,
                    "Object signatures require Int32 or concrete class references; other storage needs later lowering." + Path(parent, offset), input.Source.Name)),
            };
            ImmutableArray<IrType> parameters = [.. (isStatic ? Array.Empty<IrType>() : new[] { IrType.Reference(ownerId) }),
                .. input.Signature.Parameters.Select(p => Type(p))];
            var returned = Type(input.Signature.ReturnType, allowVoid: true);
            if (parameters.Length > IrLimits.MaximumParameters || input.Locals.Length > IrLimits.MaximumLocals)
                Fail(parent, "Object signature storage limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            if (constructor && returned != IrType.Void) Fail(parent, "Constructors require a void return.", offset);
            if (kind == ObjectMethodKind.ObjectBaseConstructor && (parameters.Length != 1 || !input.Locals.IsEmpty
                || input.Instructions.IsEmpty || input.Instructions[^1].OpCode != "ret" || input.Instructions[..^1].Any(i => i.OpCode != "nop")))
                Fail(parent, "Selected Object constructor must be an ordinary parameterless nop/ret body; nontrivial runtime intrinsics need explicit support.", offset);
            var work = new Work(resolved, input, new(key, ownerId, kind, input.Attributes, input.OwnerExternallyVisible),
                parameters, returned, [.. input.Locals.Select(l => Type(l))], parent, offset);
            _methods.Add(key, work); _pending.Enqueue(work); return work;
        }

        private Work Call(Work caller, MethodInstruction instruction)
        {
            var site = (caller.Binding.Method, instruction.Offset);
            if (_callSites.TryGetValue(site, out var cached)) return cached;
            int token = instruction.IntegerOperand ?? 0;
            if ((uint)token >> 24 is not (0x06 or 0x0a or 0x2b) || (token & 0xffffff) == 0)
                Fail(caller, "Call requires a non-nil MethodDef, MemberRef or MethodSpec.", instruction.Offset, DiagnosticIds.InvalidOperand);
            var image = caller.Resolved.DeclaringType.Definition.Assembly;
            var cacheKey = (image.ContentHash, token);
            if (!_callTargets.TryGetValue(cacheKey, out var target))
            {
                var resolved = resolver.ResolveDeclaredMethod(image, MetadataTokens.EntityHandle(token), _metadata);
                if (resolved.Value is not ResolvedMethod declared) throw new Failure(resolved.Diagnostics[0] with { Location = Location(caller, instruction.Offset), Message = resolved.Diagnostics[0].Message + Path(caller, instruction.Offset) });
                target = declared; _callTargets.Add(cacheKey, target);
            }
            var callee = Discover(target, caller, instruction.Offset);
            bool sameOwner = caller.Binding.Owner == callee.Binding.Owner;
            if (!sameOwner && ((callee.Input.Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public || !callee.Input.OwnerExternallyVisible))
                Fail(caller, "Calls require same-owner access or a public member on an externally visible owner.", instruction.Offset);
            if (instruction.OpCode == "callvirt" && callee.Binding.Kind != ObjectMethodKind.Instance)
                Fail(caller, "callvirt requires a nonvirtual ordinary instance method; constructors and static calls are unsupported.", instruction.Offset);
            if (instruction.OpCode == "newobj" && callee.Binding.Kind is not (ObjectMethodKind.Constructor or ObjectMethodKind.ObjectBaseConstructor))
                Fail(caller, "newobj requires an exact instance constructor declaration.", instruction.Offset);
            int arity = callee.Parameters.Length - (instruction.OpCode == "newobj" ? 1 : 0);
            _arguments += arity;
            if (_arguments > ProgramLimits.MaximumCallArguments) Fail(caller, "Aggregate call-argument limit exceeded.", instruction.Offset, DiagnosticIds.TranslationLimit);
            _callSites.Add(site, callee); return callee;
        }

        private ObjectFieldId Field(Work caller, MethodInstruction instruction)
        {
            int token = instruction.IntegerOperand ?? 0;
            if ((uint)token >> 24 is not (0x04 or 0x0a) || (token & 0xffffff) == 0)
                Fail(caller, "Field instruction requires a non-nil FieldDef or MemberRef.", instruction.Offset, DiagnosticIds.InvalidOperand);
            var image = caller.Resolved.DeclaringType.Definition.Assembly;
            var cacheKey = (image.ContentHash, token);
            if (!_fieldTargets.TryGetValue(cacheKey, out var field))
            {
                var resolved = resolver.ResolveDeclaredField(image, MetadataTokens.EntityHandle(token), _metadata);
                if (resolved.Value is not ResolvedField declared) throw new Failure(resolved.Diagnostics[0] with { Location = Location(caller, instruction.Offset) });
                field = declared; _fieldTargets.Add(cacheKey, field);
            }
            if (field.DeclaringType.Arguments.Count != 0) Fail(caller, "Generic field owners require later lowering.", instruction.Offset);
            return new(Include(field.DeclaringType.Definition), MetadataTokens.GetToken(field.Handle));
        }

        private ObjectTypeId TypeTarget(Work caller, MethodInstruction instruction)
        {
            int token = instruction.IntegerOperand ?? 0;
            if ((uint)token >> 24 == 0x1b)
                Fail(caller, "Class casts/type tests require a named TypeDef or TypeRef; constructed TypeSpec targets need later lowering.", instruction.Offset);
            if ((uint)token >> 24 is not (0x01 or 0x02) || (token & 0xffffff) == 0)
                Fail(caller, "Class casts/type tests require a non-nil TypeDef or TypeRef.", instruction.Offset, DiagnosticIds.InvalidOperand);
            var image = caller.Resolved.DeclaringType.Definition.Assembly;
            var key = (image.ContentHash, token);
            if (_typeTargets.TryGetValue(key, out var known)) return known;
            var resolved = resolver.ResolveType(image, MetadataTokens.EntityHandle(token));
            if (resolved.Value is not ResolvedType target)
                throw new Failure(resolved.Diagnostics[0] with { Location = Location(caller, instruction.Offset) });
            // The final catalog validates the complete target/base/field closure with
            // the shared metadata budget, including targets used only by a type test.
            var id = Include(target); _typeTargets.Add(key, id); return id;
        }

        private IrMethod Body(Work work)
        {
            var input = work.Input;
            if (input.Instructions.IsEmpty) Fail(work, "Method body is empty.", 0, DiagnosticIds.InconsistentStack);
            var partition = CilControlFlow.Partition(input, (code, message, offset) => Fail(work, message, offset, code));
            var blocks = ObjectStackFlow.Infer(input, _object, partition, TranslateBlock,
                (code, message, offset) => Fail(work, message, offset, code), Consume);
            return new(input.Source, work.Parameters, work.Return, blocks, work.Locals);

            void Consume(int amount, int offset)
            {
                if (amount > MaximumInferenceWork - _inferenceWork)
                    Fail(work, "Aggregate object stack-inference work limit exceeded.", offset, DiagnosticIds.TranslationLimit);
                _inferenceWork += amount;
            }
            IrBlock TranslateBlock(int id, int start, int end, ImmutableArray<IrType> inputs)
            {
                var statements = new List<IrStatement>(); var values = new List<IrType>(); var stack = new List<int>();
                IrTerminator? returned = null;
                int blockOffset = input.Instructions[start].Offset;
                if (inputs.Length > input.MaxStack) Fail(work, "Incoming stack exceeds declared maximum.", blockOffset, DiagnosticIds.InconsistentStack);
                Consume(inputs.Length, blockOffset);
                for (int i = 0; i < inputs.Length; i++) Push(blockOffset, inputs[i], new IrExpression.StackInput(i));
                for (int index = start; index < end; index++)
                {
                    var instruction = input.Instructions[index]; int offset = instruction.Offset;
                    Consume(1, offset);
                    string opcode = instruction.OpCode;
                    if (Int32Operations.FromOpcode(opcode) is Int32Operation operation)
                    {
                        int right = Pop(offset), left = Pop(offset);
                        if (values[left] != IrType.Int32 || values[right] != IrType.Int32) Fail(work, "Arithmetic requires Int32 operands.", offset);
                        Push(offset, IrType.Int32, new IrExpression.Binary(operation, left, right), Int32Operations.MayFault(operation));
                    }
                    else switch (opcode)
                    {
                        case "nop": break;
                        case "ldnull": Push(offset, IrType.NullReference, new IrExpression.Null()); break;
                        case "ldarg.0" or "ldarg.1" or "ldarg.2" or "ldarg.3" or "ldarg.s" or "ldarg":
                            int parameter = Variable(instruction, work.Parameters.Length); Push(offset, work.Parameters[parameter], new IrExpression.Parameter(parameter)); break;
                        case "ldloc.0" or "ldloc.1" or "ldloc.2" or "ldloc.3" or "ldloc.s" or "ldloc":
                            int local = Variable(instruction, work.Locals.Length); Push(offset, work.Locals[local], new IrExpression.Local(local)); break;
                        case "stloc.0" or "stloc.1" or "stloc.2" or "stloc.3" or "stloc.s" or "stloc":
                            Add(new IrStatement.StoreLocal(offset, Variable(instruction, work.Locals.Length), Pop(offset)), offset); break;
                        case "ldc.i4.m1" or "ldc.i4.0" or "ldc.i4.1" or "ldc.i4.2" or "ldc.i4.3" or "ldc.i4.4" or "ldc.i4.5" or "ldc.i4.6" or "ldc.i4.7" or "ldc.i4.8":
                            Push(offset, IrType.Int32, new IrExpression.Constant(opcode == "ldc.i4.m1" ? -1 : opcode[^1] - '0')); break;
                        case "ldc.i4" or "ldc.i4.s": Push(offset, IrType.Int32, new IrExpression.Constant(instruction.IntegerOperand!.Value)); break;
                        case "dup": int value = Pop(offset); stack.Add(value); stack.Add(value); break;
                        case "pop": Pop(offset); break;
                        case "not":
                            int operand = Pop(offset); if (values[operand] != IrType.Int32) Fail(work, "not requires Int32.", offset);
                            Push(offset, IrType.Int32, new IrExpression.Unary(Int32UnaryOperation.BitwiseNot, operand)); break;
                        case "ceq" or "clt" or "clt.un" or "cgt" or "cgt.un":
                            int right = Pop(offset), left = Pop(offset);
                            var comparison = opcode switch { "ceq" => Int32Comparison.Equal, "clt" => Int32Comparison.LessSigned,
                                "clt.un" => Int32Comparison.LessUnsigned, "cgt" => Int32Comparison.GreaterSigned,
                                "cgt.un" when values[left].Kind is IrTypeKind.Reference or IrTypeKind.NullReference && values[right] == IrType.NullReference => Int32Comparison.NotEqual,
                                _ => Int32Comparison.GreaterUnsigned };
                            Push(offset, IrType.Int32, new IrExpression.Compare(comparison, left, right)); break;
                        case "newobj" or "call" or "callvirt":
                            var target = Call(work, instruction); bool allocation = opcode == "newobj";
                            int arity = target.Parameters.Length - (allocation ? 1 : 0);
                            Consume(arity, offset);
                            var arguments = new int[arity];
                            for (int argument = arguments.Length - 1; argument >= 0; argument--) arguments[argument] = Pop(offset);
                            if (allocation) Push(offset, IrType.Reference(target.Binding.Owner), new IrExpression.NewObject(target.Binding.Method, [.. arguments]), true);
                            else if (target.Return == IrType.Void) Add(new IrStatement.Invoke(offset, target.Binding.Method, [.. arguments], opcode == "callvirt"), offset);
                            else Push(offset, target.Return, new IrExpression.Call(target.Binding.Method, [.. arguments], opcode == "callvirt"), true);
                            break;
                        case "castclass" or "isinst":
                            var testedType = TypeTarget(work, instruction); int tested = Pop(offset);
                            if (values[tested].Kind is not (IrTypeKind.Reference or IrTypeKind.NullReference))
                                Fail(work, "Class casts/type tests require an object reference operand.", offset);
                            Push(offset, IrType.Reference(testedType), opcode == "castclass"
                                ? new IrExpression.CastClass(testedType, tested) : new IrExpression.IsInstance(testedType, tested), true);
                            break;
                        case "ldfld":
                            var loaded = Field(work, instruction); int receiver = Pop(offset);
                            var definition = _roots[loaded.Owner];
                            _ = ReadType(definition, new());
                            _snapshotFields[loaded.Owner].TryGetValue(loaded.Token, out var field);
                            if (field is null) Fail(work, "Field is not listed by its declaring owner.", offset);
                            IrType storage = field!.Signature switch { TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 } => IrType.Int32,
                                TypeSignature.Primitive { Code: PrimitiveTypeCode.Object } => IrType.Reference(_object),
                                TypeSignature.Named { Kind: SignatureTypeKind.Class } named => IrType.Reference(Include(named.Definition)),
                                _ => throw new Failure(new(DiagnosticIds.ExecutionSignatureUnsupported, DiagnosticSeverity.Error, "Unsupported field storage.", Location(work, offset))) };
                            Push(offset, storage, new IrExpression.LoadField(loaded, receiver), true); break;
                        case "stfld":
                            var stored = Field(work, instruction); int storedValue = Pop(offset), storedReceiver = Pop(offset);
                            Add(new IrStatement.StoreField(offset, stored, storedReceiver, storedValue), offset); break;
                        case "br" or "br.s": returned = new IrJump(offset, Edge(instruction.Targets[0], offset)); break;
                        case "ret":
                            if (stack.Count != (work.Return == IrType.Void ? 0 : 1)) Fail(work, "Return has an incompatible evaluation stack.", offset, DiagnosticIds.InconsistentStack);
                            returned = work.Return == IrType.Void ? new IrVoidReturn(offset) : new IrReturn(offset, Pop(offset)); break;
                        default:
                            if (CilControlFlow.BranchComparison(opcode) is Int32Comparison condition)
                            {
                                int? branchRight = condition is Int32Comparison.Zero or Int32Comparison.Nonzero ? null : Pop(offset);
                                int branchLeft = Pop(offset);
                                returned = new IrBranch(offset, condition, branchLeft, branchRight,
                                    Edge(instruction.Targets[0], offset), Edge(offset + instruction.Length, offset));
                            }
                            else Fail(work, "Instruction '" + opcode + "' requires later object operation lowering.", offset);
                            break;
                    }
                    if (stack.Count > input.MaxStack) Fail(work, "Evaluation stack exceeds declared maximum.", offset, DiagnosticIds.InconsistentStack);
                }
                var last = input.Instructions[end - 1];
                returned ??= new IrJump(last.Offset, Edge(last.Offset + last.Length, last.Offset));
                return new(id, blockOffset, inputs, [.. statements], returned);

                IrEdge Edge(int targetOffset, int from)
                {
                    if (!partition.BlockAt.TryGetValue(targetOffset, out int target))
                        Fail(work, "Control flow falls outside this method.", from, DiagnosticIds.InvalidControlTransfer);
                    return new(target, [.. stack]);
                }

                int Variable(MethodInstruction instruction, int count)
                {
                    if (instruction.IntegerOperand is not int index || index < 0 || index >= count) Fail(work, "Argument/local index is outside its declared storage.", instruction.Offset, DiagnosticIds.InvalidOperand);
                    return instruction.IntegerOperand!.Value;
                }
                int Pop(int offset)
                {
                    if (stack.Count == 0) Fail(work, "Instruction requires an evaluation-stack operand.", offset, DiagnosticIds.InconsistentStack);
                    int value = stack[^1]; stack.RemoveAt(stack.Count - 1); return value;
                }
                void Add(IrStatement statement, int offset)
                {
                    if (statements.Count >= IrLimits.MaximumStatements) Fail(work, "IR statement limit exceeded.", offset, DiagnosticIds.TranslationLimit);
                    statements.Add(statement);
                }
                void Push(int offset, IrType type, IrExpression expression, bool throwing = false)
                {
                    int id = values.Count; values.Add(type); Add(new IrStatement.Define(new(id, type, offset, expression), throwing ? IrExceptionTarget.MethodExit : null), offset); stack.Add(id);
                }
            }
        }

        private static string Location(Work? work, int offset) => work is null ? "entry" : work.Input.Source.Name
            + " token 0x" + work.Input.Source.Token.ToString("x8", CultureInfo.InvariantCulture) + " IL_" + offset.ToString("x4", CultureInfo.InvariantCulture);
        private static string Path(Work? work, int offset)
        {
            var path = new Stack<string>();
            if (work is not null) path.Push(Location(work, offset));
            for (var current = work; current?.Parent is not null; current = current.Parent) path.Push(Location(current.Parent, current.Offset));
            return path.Count == 0 ? "" : " Reachable via: " + string.Join(" -> ", path) + ".";
        }
        private static void Fail(Work? work, string message, int offset, string code = DiagnosticIds.ExecutionUnsupported) =>
            throw new Failure(new(code, DiagnosticSeverity.Error, message + Path(work, offset), Location(work, offset)));
    }
}
