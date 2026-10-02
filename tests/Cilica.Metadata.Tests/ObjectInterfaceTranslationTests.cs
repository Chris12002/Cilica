using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ObjectInterfaceTranslationTests
{
    private static ObjectIrProgram Translate(string name)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image = resolver.OpenInput(typeof(InterfaceCallProbe).Assembly.Location).Value!;
        var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var entry = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(InterfaceCallProbe).GetMethod(name)!.MetadataToken);
        var result = ObjectTranslator.Translate(resolver, image, entry, runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        return result.Program!;
    }

    [Fact]
    public void Interface_calls_match_dotnet_for_hiding_overrides_reimplementation_explicit_and_diamond_maps()
    {
        var program = Translate(nameof(InterfaceCallProbe.Run));
        var plan = program.InterfaceDispatch!;
        Assert.Equal(3, plan.Declarations.Length);
        Assert.DoesNotContain(program.Flow.Methods, m => plan.Declarations.Any(d => IrMethodId.From(d.Source) == IrMethodId.From(m.Source)));
        Assert.Contains(plan.Methods, m => !program.Flow.Methods.Any(b => IrMethodId.From(b.Source) == m.Binding.Method));
        ObjectIrValidator.Validate(program); // Still detached after the resolver has been disposed.
        ObjectInterfacePlanValidator.Validate(JsonSerializer.Deserialize<ObjectInterfaceDispatchPlan>(JsonSerializer.Serialize(plan))!);
        for (int kind = 0; kind < 12; kind++)
            foreach (int value in new[] { 0, -19, int.MinValue, int.MaxValue })
                Assert.Equal(InterfaceCallProbe.Run(kind, value), new Evaluator(program).Run(kind, value));
        Assert.Throws<ArgumentException>(() => ObjectCppEmitter.EmitProgram(program));
    }

    [Theory]
    [InlineData(nameof(InterfaceCallProbe.OtherContract))]
    [InlineData(nameof(InterfaceCallProbe.Diamond))]
    [InlineData(nameof(InterfaceCallProbe.ReferenceFlow))]
    [InlineData(nameof(InterfaceCallProbe.Recursive))]
    [InlineData(nameof(InterfaceCallProbe.MetadataOnlySlot))]
    [InlineData(nameof(InterfaceCallProbe.MixedClassCall))]
    public void Body_discovery_reference_results_void_calls_and_recursive_interface_calls_match_dotnet(string name)
    {
        var program = Translate(name);
        Func<int, int> oracle = name switch
        {
            nameof(InterfaceCallProbe.OtherContract) => InterfaceCallProbe.OtherContract,
            nameof(InterfaceCallProbe.Diamond) => InterfaceCallProbe.Diamond,
            nameof(InterfaceCallProbe.ReferenceFlow) => InterfaceCallProbe.ReferenceFlow,
            nameof(InterfaceCallProbe.MetadataOnlySlot) => InterfaceCallProbe.MetadataOnlySlot,
            nameof(InterfaceCallProbe.MixedClassCall) => InterfaceCallProbe.MixedClassCall,
            _ => InterfaceCallProbe.Recursive,
        };
        foreach (int value in new[] { 0, 1, 7, 19 }) Assert.Equal(oracle(value), new Evaluator(program).Run(value));
        var roots = ObjectRootAssignment.Assign(program);
        Assert.Equal(program.Flow.Methods.Length, roots.Length);
        if (name == nameof(InterfaceCallProbe.ReferenceFlow))
        {
            Assert.Contains(program.Flow.Types.Types, t => t.Source.Name.EndsWith("SecondFrameBox", StringComparison.Ordinal));
            Assert.Contains(program.InterfaceDispatch!.Declarations, d => d.Source.Name.Contains("IExactSecond", StringComparison.Ordinal));
            Assert.Contains(program.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Statements),
                s => s is IrStatement.Invoke { VirtualDispatch: true });
            Assert.Contains(program.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values),
                v => v.Type.Class is { } type && program.Flow.Types.Interfaces.Any(i => ObjectTypeId.From(i.Source) == type)
                    && v.Expression is IrExpression.Call);
            var method = program.Flow.Methods.Single(m => IrMethodId.From(m.Source) == program.Flow.Entry);
            var assigned = roots.Single(r => r.Method == program.Flow.Entry);
            Assert.All(method.Locals.Select((t, index) => (t, index)).Where(p => p.t.Kind == IrTypeKind.Reference),
                p => Assert.Contains(assigned.Slots, s => s.Kind == ObjectRootKind.Local && s.Index == p.index));
            foreach (var block in method.Blocks)
                foreach (var value in block.Values.Where(v => v.Type.Kind is IrTypeKind.Reference or IrTypeKind.NullReference))
                    Assert.Contains(assigned.Slots, s => s.Kind == ObjectRootKind.Value && s.Index == value.Id && s.Block == block.Id);
            Assert.Equal(method.Blocks.SelectMany(b => b.Statements).Count(s => s is IrStatement.Invoke
                or IrStatement.Define { Value.Expression: IrExpression.Call or IrExpression.NewObject }), assigned.Safepoints.Length);
        }
        if (name == nameof(InterfaceCallProbe.MetadataOnlySlot))
        {
            Assert.DoesNotContain(program.Flow.Methods, m => m.Source.Token == typeof(FrameSlotBase).GetMethod(nameof(FrameSlotBase.Read))!.MetadataToken);
            Assert.Contains(program.InterfaceDispatch!.Methods, m => m.Binding.Method.Token == typeof(FrameSlotBase).GetMethod(nameof(FrameSlotBase.Read))!.MetadataToken);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Argument_fault_precedes_interface_receiver_null_check(int divisor)
    {
        var program = Translate(nameof(InterfaceCallProbe.NullCall));
        Assert.Empty(Assert.Single(program.InterfaceDispatch!.Dispatch).Targets);
        Assert.Empty(program.InterfaceDispatch.Methods);
        var expected = Record.Exception(() => InterfaceCallProbe.NullCall(divisor));
        var actual = Record.Exception(() => new Evaluator(program).Run(divisor));
        Assert.NotNull(expected); Assert.Equal(expected.GetType(), actual!.GetType());
    }

    [Theory]
    [InlineData("direct")]
    [InlineData("default")]
    [InlineData("static")]
    [InlineData("generic")]
    [InlineData("missing")]
    [InlineData("unsupported_body")]
    [InlineData("cast")]
    [InlineData("isinst")]
    public void Unsupported_reachable_interface_semantics_return_actionable_errors_without_partial_ir(string kind)
    {
        var builder = new ResolutionAssemblyBuilder(new("InterfaceCalls", new Version(1, 0, 0, 0)));
        var rootObject = builder.Type("System", "Object");
        var contract = builder.Type("Example", "Read", arity: kind == "generic" ? 1 : 0,
            attributes: TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        const MethodAttributes abstractFlags = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Abstract | MethodAttributes.NewSlot;
        var declaration = builder.Method("Read", kind == "static" ? [0, 0, 8] : [0x20, 0, 8],
            attributes: kind == "static" ? MethodAttributes.Public | MethodAttributes.Static : kind == "default" ? abstractFlags & ~MethodAttributes.Abstract : abstractFlags,
            emit: kind == "default" ? il => { il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret); } : null);
        var receiver = builder.Type("Example", "Receiver", baseType: rootObject); builder.Implements(receiver, contract);
        if (kind != "missing") builder.Method("Read", [0x20, 0, 8], attributes: MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot,
            emit: il =>
            {
                if (kind == "unsupported_body") { il.LoadString(builder.String("unsupported")); il.OpCode(ILOpCode.Pop); }
                il.LoadConstantI4(7); il.OpCode(ILOpCode.Ret);
            });
        builder.Type("Example", "Driver", baseType: rootObject);
        var entry = builder.Method("Run", [0, 0, 8], locals: [7, 1, 0x12, (byte)(MetadataTokens.GetRowNumber(receiver) * 4)], emit: il =>
        {
            if (kind != "static") il.OpCode(ILOpCode.Ldnull);
            if (kind is "cast" or "isinst")
            {
                il.OpCode(kind == "cast" ? ILOpCode.Castclass : ILOpCode.Isinst); il.Token(contract);
                il.OpCode(ILOpCode.Pop); il.LoadConstantI4(1);
            }
            else { il.OpCode(kind is "direct" or "static" ? ILOpCode.Call : ILOpCode.Callvirt); il.Token(declaration); }
            il.OpCode(ILOpCode.Ret);
        });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("calls.dll", builder.Build())).Value!;
        var result = ObjectTranslator.Translate(resolver, image, entry, image);
        Assert.Null(result.Program); var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains("Run", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("IL_", diagnostic.Message, StringComparison.Ordinal);
        if (kind is "cast" or "isinst") Assert.Contains("Interface casts/type tests", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ordinary_static_calls_do_not_read_unused_owner_field_storage()
    {
        var builder = new ResolutionAssemblyBuilder(new("StaticOwner", new Version(1, 0, 0, 0)));
        builder.Type("System", "Object");
        builder.Type("Example", "Helpers", attributes: TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        builder.Field("MalformedUnusedField", 0);
        var callee = builder.Method("Get", [0, 0, 8], emit: il => { il.LoadConstantI4(9); il.OpCode(ILOpCode.Ret); });
        builder.Type("Example", "Driver");
        var entry = builder.Method("Run", [0, 0, 8], emit: il => { il.Call(callee); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("static.dll", builder.Build())).Value!;
        var result = ObjectTranslator.Translate(resolver, image, entry, image);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(9, new Evaluator(result.Program!).Run());
    }

    [Fact]
    public void Declarations_facts_and_executable_bodies_share_the_method_budget()
    {
        var program = Translate(nameof(InterfaceCallProbe.OtherContract));
        var entry = program.Flow.Methods.Single(m => IrMethodId.From(m.Source) == program.Flow.Entry);
        var binding = program.Bindings.Single(b => b.Method == program.Flow.Entry);
        var methods = program.Flow.Methods; var bindings = program.Bindings;
        for (int token = 0x06001000; methods.Length < ProgramLimits.MaximumMethods; token++)
        {
            var source = entry.Source with { Token = token };
            methods = methods.Add(entry with { Source = source });
            bindings = bindings.Add(binding with { Method = IrMethodId.From(source) });
        }
        program = program with { Flow = program.Flow with { Methods = methods }, Bindings = bindings };
        Assert.Contains("256-method limit", Assert.Throws<ArgumentException>(() => ObjectIrValidator.Validate(program)).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Constructor_interface_calls_require_base_initialization_first(bool beforeBase)
    {
        var builder = new ResolutionAssemblyBuilder(new("ConstructorInterface", new Version(1, 0, 0, 0)));
        var rootObject = builder.Type("System", "Object");
        const MethodAttributes ctorFlags = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
        var baseConstructor = builder.Method(".ctor", [0x20, 0, 1], attributes: ctorFlags, emit: il => il.OpCode(ILOpCode.Ret));
        var contract = builder.Type("Example", "Init", attributes: TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        var declaration = builder.Method("Init", [0x20, 0, 1], attributes: MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Abstract | MethodAttributes.NewSlot);
        var receiver = builder.Type("Example", "Receiver", baseType: rootObject); builder.Implements(receiver, contract);
        builder.Method("Init", [0x20, 0, 1], attributes: MethodAttributes.Public, emit: il => il.OpCode(ILOpCode.Ret));
        var constructor = builder.Method(".ctor", [0x20, 0, 1], attributes: ctorFlags, emit: il =>
        {
            if (beforeBase) InterfaceCall();
            il.LoadArgument(0); il.Call(baseConstructor);
            if (!beforeBase) InterfaceCall();
            il.OpCode(ILOpCode.Ret);
            void InterfaceCall() { il.LoadArgument(0); il.OpCode(ILOpCode.Callvirt); il.Token(declaration); }
        });
        builder.Type("Example", "Driver");
        var entry = builder.Method("Run", [0, 0, 8], emit: il =>
        {
            il.OpCode(ILOpCode.Newobj); il.Token(constructor); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret);
        });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("constructor.dll", builder.Build())).Value!;
        var result = ObjectTranslator.Translate(resolver, image, entry, image);
        Assert.Equal(!beforeBase, result.Success);
        if (beforeBase)
        {
            Assert.Null(result.Program);
            Assert.Contains("uninitialized constructor receiver", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(1, new Evaluator(result.Program!).Run());
            Assert.Contains(ObjectRootAssignment.Assign(result.Program!), r => r.Safepoints.Any(s => s.Kind == ObjectSafepointKind.Call));
        }
    }

    [Theory]
    [InlineData("no_plan")]
    [InlineData("missing_body")]
    [InlineData("body_signature")]
    [InlineData("body_binding")]
    [InlineData("catalog")]
    [InlineData("unused_map")]
    [InlineData("direct")]
    [InlineData("no_null_check")]
    [InlineData("no_fault_edge")]
    [InlineData("wrong_result")]
    [InlineData("wrong_argument")]
    [InlineData("private_direct")]
    [InlineData("bodyless_body")]
    public void Forged_executable_interface_ir_is_rejected(string kind)
    {
        var program = Translate(nameof(InterfaceCallProbe.OtherContract));
        var plan = program.InterfaceDispatch!;
        var selected = plan.Dispatch[0].Targets[0].Implementation;
        var bodyIndex = program.Flow.Methods.IndexOf(program.Flow.Methods.Single(m => IrMethodId.From(m.Source) == selected));
        var entryIndex = program.Flow.Methods.IndexOf(program.Flow.Methods.Single(m => IrMethodId.From(m.Source) == program.Flow.Entry));
        var entry = program.Flow.Methods[entryIndex];
        switch (kind)
        {
            case "no_plan": program = program with { InterfaceDispatch = null }; break;
            case "missing_body": program = program with { Flow = program.Flow with { Methods = program.Flow.Methods.RemoveAt(bodyIndex) },
                Bindings = [.. program.Bindings.Where(b => b.Method != selected)] }; break;
            case "body_signature": program = program with { Flow = program.Flow with { Methods = program.Flow.Methods.SetItem(bodyIndex,
                program.Flow.Methods[bodyIndex] with { Parameters = [IrType.Reference(plan.Methods.Single(m => m.Binding.Method == selected).Binding.Owner)] }) } }; break;
            case "body_binding": program = program with { Bindings = [.. program.Bindings.Select(b => b.Method == selected ? b with { OwnerExternallyVisible = false } : b)] }; break;
            case "catalog": program = program with { InterfaceDispatch = plan with { Types = plan.Types with { Roots = [plan.Types.ObjectBase] } } }; break;
            case "bodyless_body":
                var declaration = plan.Declarations[0];
                program = program with { Flow = program.Flow with { Methods = program.Flow.Methods.Add(new(declaration.Source,
                    declaration.Parameters, declaration.ReturnType, [], [])) }, Bindings = program.Bindings.Add(new(IrMethodId.From(declaration.Source),
                    declaration.Owner, ObjectMethodKind.Instance, declaration.Attributes, true)) }; break;
            case "unused_map": entry = entry with { Blocks = [.. entry.Blocks.Select(b => b with { Statements = [.. b.Statements.Select(s =>
                s is IrStatement.Define { Value.Expression: IrExpression.Call } d ? d with { Value = d.Value with { Expression = new IrExpression.Constant(1) }, Exceptional = null } : s)] })] }; break;
            default:
                entry = entry with { Blocks = [.. entry.Blocks.Select(b => b with { Statements = [.. b.Statements.Select(s =>
                    s is IrStatement.Define { Value.Expression: IrExpression.Call } d ? Change(d) : s)] })] }; break;
        }
        if (kind is "unused_map" or "direct" or "no_null_check" or "no_fault_edge" or "wrong_result" or "wrong_argument" or "private_direct")
            program = program with { Flow = program.Flow with { Methods = program.Flow.Methods.SetItem(entryIndex, entry) } };
        Assert.Throws<ArgumentException>(() => ObjectIrValidator.Validate(program));
        Assert.Throws<ArgumentException>(() => ObjectRootAssignment.Assign(program));
        IrStatement.Define Change(IrStatement.Define d)
        {
            var call = (IrExpression.Call)d.Value.Expression;
            return kind switch
            {
                "direct" => d with { Value = d.Value with { Expression = call with { VirtualDispatch = false } } },
                "no_null_check" => d with { Value = d.Value with { Expression = call with { NullCheckReceiver = false } } },
                "no_fault_edge" => d with { Exceptional = null },
                "wrong_argument" => d with { Value = d.Value with { Expression = call with { Arguments = call.Arguments.SetItem(1, call.Arguments[0]) } } },
                "private_direct" => d with { Value = d.Value with { Expression = call with { Target = selected, VirtualDispatch = false } } },
                _ => d with { Value = d.Value with { Type = IrType.Reference(plan.Types.ObjectBase) } },
            };
        }
    }

    // Bounded test oracle for independently authored fixtures. It is not a Cilica runtime or native execution evidence.
    private sealed class Evaluator(ObjectIrProgram program)
    {
        private sealed record Instance(ObjectTypeId Type, Dictionary<ObjectFieldId, object?> Fields);
        private readonly Dictionary<IrMethodId, IrMethod> _methods = program.Flow.Methods.ToDictionary(m => IrMethodId.From(m.Source));
        private readonly Dictionary<IrMethodId, ObjectMethodBinding> _bindings = program.Bindings.ToDictionary(b => b.Method);
        private int _steps;
        internal int Run(params int[] arguments) => (int)Call(program.Flow.Entry, [.. arguments.Cast<object?>()], false, false, 0)!;
        private object? Call(IrMethodId target, object?[] arguments, bool nullCheck, bool dispatch, int depth)
        {
            if (depth > 64) throw new InvalidOperationException("Authored evaluator depth limit.");
            if (nullCheck && arguments[0] is null) throw new NullReferenceException();
            if (dispatch)
            {
                var receiver = ((Instance)arguments[0]!).Type;
                var contract = program.InterfaceDispatch!.Dispatch.SingleOrDefault(m => m.Declaration == target);
                target = contract is not null ? contract.Targets.Single(t => t.Receiver == receiver).Implementation
                    : program.Dispatch.Single(m => m.Declaration == target).Targets.Single(t => t.Receiver == receiver).Implementation;
            }
            var method = _methods[target];
            var locals = method.Locals.Select(t => t == IrType.Int32 ? (object)0 : null).ToArray();
            int blockId = 0; object?[] inputs = [];
            while (true)
            {
                var block = method.Blocks.Single(b => b.Id == blockId); var values = new List<object?>();
                foreach (var statement in block.Statements)
                {
                    if (++_steps > 65536) throw new InvalidOperationException("Authored evaluator step limit.");
                    switch (statement)
                    {
                        case IrStatement.Define d: values.Add(Value(d.Value.Expression)); break;
                        case IrStatement.StoreLocal s: locals[s.Local] = values[s.Value]; break;
                        case IrStatement.StoreField s: ((Instance)values[s.Receiver]!).Fields[s.Field] = values[s.Value]; break;
                        case IrStatement.Invoke s: Invoke(s.Target, s.Arguments, s.NullCheckReceiver, s.VirtualDispatch); break;
                        default: throw new InvalidOperationException("Outside authored evaluator profile.");
                    }
                }
                IrEdge edge;
                switch (block.Terminator)
                {
                    case IrReturn r: return values[r.Value];
                    case IrVoidReturn: return null;
                    case IrJump j: edge = j.Edge; break;
                    case IrBranch b: edge = Compare(b.Comparison, values[b.Left], b.Right is { } right ? values[right] : 0) ? b.Taken : b.NotTaken; break;
                    default: throw new InvalidOperationException("Outside authored evaluator profile.");
                }
                blockId = edge.Target; inputs = [.. edge.Arguments.Select(a => values[a])];
                object? Invoke(IrMethodId id, ImmutableArray<int> args, bool check, bool virtualCall) => Call(id, [.. args.Select(a => values[a])], check, virtualCall, depth + 1);
                object? Value(IrExpression expression) => expression switch
                {
                    IrExpression.Constant c => c.Value,
                    IrExpression.Null => null,
                    IrExpression.Parameter p => arguments[p.Index],
                    IrExpression.Local l => locals[l.Index],
                    IrExpression.StackInput s => inputs[s.Index],
                    IrExpression.Call c => Invoke(c.Target, c.Arguments, c.NullCheckReceiver, c.VirtualDispatch),
                    IrExpression.NewObject n => Allocate(n),
                    IrExpression.LoadField f => ((Instance)values[f.Receiver]!).Fields[f.Field],
                    IrExpression.Compare c => Compare(c.Comparison, values[c.Left], values[c.Right]) ? 1 : 0,
                    IrExpression.Binary b => Binary(b.Operation, (int)values[b.Left]!, (int)values[b.Right]!),
                    _ => throw new InvalidOperationException("Outside authored evaluator profile."),
                };
                Instance Allocate(IrExpression.NewObject n)
                {
                    var type = _bindings[n.Constructor].Owner; var fields = new Dictionary<ObjectFieldId, object?>();
                    for (ObjectTypeId? current = type; current is not null;)
                    {
                        var layout = program.Flow.Types.Types.Single(t => ObjectTypeId.From(t.Source) == current);
                        foreach (var field in layout.Fields) fields.Add(field.Id, field.Storage is ObjectStorage.Int32 ? 0 : null);
                        current = layout.BaseType;
                    }
                    var instance = new Instance(type, fields);
                    Call(n.Constructor, [instance, .. n.Arguments.Select(a => values[a])], false, false, depth + 1); return instance;
                }
            }
        }
        private static int Binary(Int32Operation operation, int left, int right) => operation switch
        {
            Int32Operation.AddWrapping => unchecked(left + right),
            Int32Operation.SubtractWrapping => unchecked(left - right),
            Int32Operation.DivideSigned => left / right,
            _ => throw new InvalidOperationException("Outside authored evaluator arithmetic profile."),
        };
        private static bool Compare(Int32Comparison comparison, object? left, object? right) => comparison switch
        {
            Int32Comparison.Equal => left is int l && right is int r ? l == r : ReferenceEquals(left, right),
            Int32Comparison.NotEqual => left is int l && right is int r ? l != r : !ReferenceEquals(left, right),
            Int32Comparison.Zero => left is null || left is int zero && zero == 0,
            Int32Comparison.Nonzero => left is not null && (left is not int n || n != 0),
            _ => throw new InvalidOperationException("Outside authored evaluator comparison profile."),
        };
    }
}
