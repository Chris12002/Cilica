using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.Emit;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ObjectTranslationConformanceTests
{
    [Theory]
    [InlineData(3, 7)]
    [InlineData(0, 0)]
    [InlineData(-17, 23)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(int.MinValue, -1)]
    public void Authored_class_mutation_reference_fields_and_void_calls_match_dotnet(int left, int right)
    {
        int expected = ObjectProbe.Run(left, right); // Authored test oracle, never run by the compiler.
        string runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        using var resolver = AssemblyResolver.Create([runtimeDirectory]).Value!;
        var image = resolver.OpenInput(typeof(ObjectProbe).Assembly.Location).Value!;
        var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ObjectProbe).GetMethod(nameof(ObjectProbe.Run))!.MetadataToken);
        var result = ObjectTranslator.Translate(resolver, image, method, runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(expected, new Evaluator(result.Program!).Run(left, right));
    }

    [Fact]
    public void Authored_null_reference_field_load_matches_dotnet_fault()
    {
        Assert.Throws<NullReferenceException>(() => ObjectProbe.ReadNull(7));
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image = resolver.OpenInput(typeof(ObjectProbe).Assembly.Location).Value!;
        var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ObjectProbe).GetMethod(nameof(ObjectProbe.ReadNull))!.MetadataToken);
        var result = ObjectTranslator.Translate(resolver, image, method, runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Throws<NullReferenceException>(() => new Evaluator(result.Program!).Run(7));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public void Null_identity_comparison_lowering_matches_authored_dotnet_il(bool inequality, int expected)
    {
        var oracle = new DynamicMethod("AuthoredNullCompare", typeof(int), Type.EmptyTypes);
        var code = oracle.GetILGenerator(); code.Emit(OpCodes.Ldnull); code.Emit(OpCodes.Ldnull);
        code.Emit(inequality ? OpCodes.Cgt_Un : OpCodes.Ceq); code.Emit(OpCodes.Ret);
        Assert.Equal(expected, oracle.CreateDelegate<Func<int>>()());
        var b = new ResolutionAssemblyBuilder(new("NullCompare", new Version(1, 0, 0, 0)));
        var objectBase = b.Type("System", "Object"); b.Type("Test", "Driver", baseType: objectBase);
        var root = b.Method("Run", [0, 0, 8], emit: il =>
        {
            il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ldnull);
            il.OpCode(inequality ? ILOpCode.Cgt_un : ILOpCode.Ceq); il.OpCode(ILOpCode.Ret);
        });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("probe.dll", b.Build())).Value!;
        var result = ObjectTranslator.Translate(resolver, image, root, image);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(expected, new Evaluator(result.Program!).Run());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Direct_call_on_null_preserves_nonvirtual_callvirt_null_check_distinction(bool nullCheck)
    {
        // Execute only this explicitly authored three-instruction oracle in the test process.
        var oracle = new DynamicMethod("AuthoredNullCall", typeof(int), Type.EmptyTypes);
        var code = oracle.GetILGenerator(); code.Emit(OpCodes.Ldnull);
        code.Emit(nullCheck ? OpCodes.Callvirt : OpCodes.Call, typeof(ProbeNode).GetMethod(nameof(ProbeNode.Constant))!);
        code.Emit(OpCodes.Ret); var reference = oracle.CreateDelegate<Func<int>>();
        if (nullCheck) Assert.Throws<NullReferenceException>(() => reference());
        else Assert.Equal(17, reference());
        var b = new ResolutionAssemblyBuilder(new("NullCall", new Version(1, 0, 0, 0)));
        var objectBase = b.Type("System", "Object");
        b.Type("Test", "Node", baseType: objectBase);
        var constant = b.Method("Constant", [0x20, 0, 8], attributes: MethodAttributes.Public,
            emit: il => { il.LoadConstantI4(17); il.OpCode(ILOpCode.Ret); });
        b.Type("Test", "Driver", baseType: objectBase);
        var root = b.Method("Run", [0, 0, 8], emit: il =>
        {
            il.OpCode(ILOpCode.Ldnull); il.OpCode(nullCheck ? ILOpCode.Callvirt : ILOpCode.Call); il.Token(constant); il.OpCode(ILOpCode.Ret);
        });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var assembly = resolver.OpenInput(files.Write("probe.dll", b.Build())).Value!;
        var result = ObjectTranslator.Translate(resolver, assembly, root, assembly);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var evaluator = new Evaluator(result.Program!);
        if (nullCheck) Assert.Throws<NullReferenceException>(() => evaluator.Run());
        else Assert.Equal(17, evaluator.Run());
    }

    // Restricted to these independently authored probes. This is not Cilica's runtime or native execution evidence.
    internal sealed class Evaluator(ObjectIrProgram program)
    {
        private sealed record ObjectValue(Dictionary<ObjectFieldId, object?> Fields);
        private readonly Dictionary<IrMethodId, IrMethod> _methods = program.Flow.Methods.ToDictionary(m => IrMethodId.From(m.Source));
        private readonly Dictionary<IrMethodId, ObjectMethodBinding> _bindings = program.Bindings.ToDictionary(b => b.Method);
        private int _steps;

        internal int Run(params int[] parameters) => (int)Call(program.Flow.Entry, [.. parameters.Cast<object?>()], 0)!;
        private object? Call(IrMethodId id, object?[] arguments, int depth)
        {
            if (depth >= 64) throw new InvalidOperationException("Authored evaluator depth limit.");
            var method = _methods[id]; var block = Assert.Single(method.Blocks);
            var locals = method.Locals.Select(t => t == IrType.Int32 ? (object)0 : null).ToArray();
            var values = new List<object?>();
            foreach (var statement in block.Statements)
            {
                if (++_steps > 65536) throw new InvalidOperationException("Authored evaluator step limit.");
                switch (statement)
                {
                    case IrStatement.Define define:
                        object? value = define.Value.Expression switch
                        {
                            IrExpression.Constant constant => constant.Value,
                            IrExpression.Null => null,
                            IrExpression.Parameter parameter => arguments[parameter.Index],
                            IrExpression.Local local => locals[local.Index],
                            IrExpression.StringLiteral literal => new string(literal.CodeUnits.Select(c => (char)c).ToArray()),
                            IrExpression.StringLength text => ((string)values[text.Operand]!).Length,
                            IrExpression.StringCodeUnit text => (int)((string)values[text.Operand]!)[(int)values[text.Index]!],
                            IrExpression.StringEquals text => string.Equals((string?)values[text.Left], (string?)values[text.Right], StringComparison.Ordinal) ? 1 : 0,
                            IrExpression.StringConcat text => string.Concat((string?)values[text.Left], (string?)values[text.Right]),
                            IrExpression.NewObject created => Allocate(created),
                            IrExpression.NewArray array => array.ArrayType.ArrayElement!.Kind == ObjectArrayElementKind.Int32
                                ? (object)new int[(int)values[array.Length]!] : new object?[(int)values[array.Length]!],
                            IrExpression.ArrayLength length => ((Array)values[length.Array]!).Length,
                            IrExpression.LoadElement load => ((Array)values[load.Array]!).GetValue((int)values[load.Index]!),
                            IrExpression.LoadField field => Receiver(values[field.Receiver]).Fields[field.Field],
                            IrExpression.Call called => Invoke(called.Target, called.Arguments.Select(a => values[a]).ToArray(), called.NullCheckReceiver),
                            IrExpression.Compare { Comparison: Int32Comparison.Equal } compare => Same(values[compare.Left], values[compare.Right]) ? 1 : 0,
                            IrExpression.Compare { Comparison: Int32Comparison.NotEqual } compare => Same(values[compare.Left], values[compare.Right]) ? 0 : 1,
                            IrExpression.Binary { Operation: Int32Operation.AddWrapping } add => unchecked((int)values[add.Left]! + (int)values[add.Right]!),
                            _ => throw new InvalidOperationException("Outside authored evaluator profile."),
                        };
                        values.Add(value); break;
                    case IrStatement.StoreElement store: ((Array)values[store.Array]!).SetValue(values[store.Value], (int)values[store.Index]!); break;
                    case IrStatement.StoreLocal store: locals[store.Local] = values[store.Value]; break;
                    case IrStatement.StoreField store: Receiver(values[store.Receiver]).Fields[store.Field] = values[store.Value]; break;
                    case IrStatement.Invoke invoked: Invoke(invoked.Target, invoked.Arguments.Select(a => values[a]).ToArray(), invoked.NullCheckReceiver); break;
                    default: throw new InvalidOperationException("Outside authored evaluator profile.");
                }
            }
            return block.Terminator is IrReturn returned ? values[returned.Value] : null;

            object? Invoke(IrMethodId target, object?[] parameters, bool nullCheck)
            {
                if (nullCheck && parameters[0] is null) throw new NullReferenceException();
                return Call(target, parameters, depth + 1);
            }
            ObjectValue Allocate(IrExpression.NewObject created)
            {
                var type = _bindings[created.Constructor].Owner;
                var layout = program.Flow.Types.Types.Single(t => ObjectTypeId.From(t.Source) == type);
                var instance = new ObjectValue(layout.Fields.ToDictionary(f => f.Id, f => f.Storage is ObjectStorage.Int32 ? (object)0 : null));
                Invoke(created.Constructor, [instance, .. created.Arguments.Select(a => values[a])], false);
                return instance;
            }
        }
        private static bool Same(object? left, object? right) => left is int a && right is int b ? a == b : ReferenceEquals(left, right);
        private static ObjectValue Receiver(object? value) => value as ObjectValue ?? throw new NullReferenceException();
    }
}
