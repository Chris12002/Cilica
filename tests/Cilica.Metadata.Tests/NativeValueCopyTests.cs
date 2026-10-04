using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeValueCopyTests
{
    internal static (ValueIrProgram Program, ImmutableArray<ObjectMethodBinding> Bindings) Model(bool swap)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(ValueCopyProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var catalog = ValueModelBuilder.BuildCatalog(resolver,
            [resolver.ResolveType(input, (TypeDefinitionHandle)MetadataTokens.EntityHandle(typeof(ValueLeaf).MetadataToken)).Value!], library);
        Assert.True(catalog.Success, string.Join("; ", catalog.Diagnostics));
        var types = catalog.Catalog!; var value = types.Roots[0]; var type = IrType.Value(value);
        var field = types.Types.Single(t => ObjectTypeId.From(t.Source) == value).Fields.Single(f => f.Name == "Number").Id;
        MethodSource Source(string name)
        {
            var method = typeof(ValueCopyProbe).GetMethod(name)!;
            return new(input.Identity.DisplayName, input.ContentHash, method.MetadataToken, method.Name);
        }
        static IrStatement.Define D(int id, IrType type, IrExpression expression, int offset = 0, bool fault = false) =>
            new(new(id, type, offset, expression), fault ? IrExceptionTarget.MethodExit : null);
        var read = new IrMethod(Source("Read"), [type], IrType.Int32,
            [new(0, 0, [], [D(0, type, new IrExpression.Parameter(0)), D(1, IrType.Int32, new IrExpression.ValueField(field, 0))], new IrReturn(1, 1))], []);
        var readId = IrMethodId.From(read.Source);
        var initial = ImmutableArray.Create<IrStatement>(D(0, IrType.Int32, new IrExpression.Parameter(0)),
            D(1, type, new IrExpression.DefaultValue(value)), D(2, type, new IrExpression.WithValueField(field, 1, 0)),
            D(3, IrType.Int32, new IrExpression.Constant(7)));
        IrMethod entry;
        if (!swap)
            entry = new(Source("Copy"), [IrType.Int32], IrType.Int32,
                [new(0, 0, [], [.. initial, new IrStatement.StoreLocal(0, 0, 2), D(4, type, new IrExpression.Local(0)),
                    D(5, type, new IrExpression.WithValueField(field, 4, 3)), new IrStatement.StoreLocal(0, 0, 5),
                    D(6, type, new IrExpression.Local(0)), D(7, IrType.Int32, new IrExpression.Call(readId, [4]), fault: true),
                    D(8, IrType.Int32, new IrExpression.Call(readId, [6]), fault: true),
                    D(9, IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, 7, 8))], new IrReturn(1, 9))], [type]);
        else
            entry = new(Source("Swap"), [IrType.Int32], IrType.Int32,
                [new(0, 0, [], [.. initial, D(4, type, new IrExpression.WithValueField(field, 1, 3)), D(5, IrType.Int32, new IrExpression.Constant(3))],
                    new IrJump(1, new(1, [2, 4, 5]))),
                 new(1, 10, [type, type, IrType.Int32],
                    [D(0, type, new IrExpression.StackInput(0), 10), D(1, type, new IrExpression.StackInput(1), 10),
                     D(2, IrType.Int32, new IrExpression.StackInput(2), 10)],
                    new IrBranch(11, Int32Comparison.Nonzero, 2, null, new(2, [0, 1, 2]), new(3, [0, 1]))),
                 new(2, 20, [type, type, IrType.Int32],
                    [D(0, type, new IrExpression.StackInput(0), 20), D(1, type, new IrExpression.StackInput(1), 20),
                     D(2, IrType.Int32, new IrExpression.StackInput(2), 20), D(3, IrType.Int32, new IrExpression.Constant(1), 20),
                     D(4, IrType.Int32, new IrExpression.Binary(Int32Operation.SubtractWrapping, 2, 3), 20)], new IrJump(21, new(1, [1, 0, 4]))),
                 new(3, 30, [type, type], [D(0, type, new IrExpression.StackInput(0), 30), D(1, type, new IrExpression.StackInput(1), 30),
                     D(2, IrType.Int32, new IrExpression.Call(readId, [0]), 30, true), D(3, IrType.Int32, new IrExpression.Call(readId, [1]), 30, true),
                     D(4, IrType.Int32, new IrExpression.Constant(10), 30),
                     D(5, IrType.Int32, new IrExpression.Binary(Int32Operation.MultiplyWrapping, 2, 4), 30),
                     D(6, IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, 5, 3), 30)], new IrReturn(31, 6))], []);
        var program = new ValueIrProgram(IrMethodId.From(entry.Source), [entry, read], types);
        var owner = new ObjectTypeId(input.ContentHash, typeof(ValueCopyProbe).MetadataToken);
        var bindings = program.Methods.Select(m => new ObjectMethodBinding(IrMethodId.From(m.Source), owner,
            ObjectMethodKind.Static, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, true)).ToImmutableArray();
        return (program, bindings);
    }

    private static (ValueIrProgram Program, ImmutableArray<ObjectMethodBinding> Bindings) NestedModel()
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(ValueCopyProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result = ValueModelBuilder.BuildCatalog(resolver,
            [resolver.ResolveType(input, (TypeDefinitionHandle)MetadataTokens.EntityHandle(typeof(ValuePair).MetadataToken)).Value!], library);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics)); var catalog = result.Catalog!;
        var outer = catalog.Types.Single(t => t.Source.Token == typeof(ValuePair).MetadataToken);
        var leaf = catalog.Types.Single(t => t.Source.Token == typeof(ValueLeaf).MetadataToken);
        var pair = IrType.Value(ObjectTypeId.From(outer.Source)); var item = IrType.Value(ObjectTypeId.From(leaf.Source));
        var left = outer.Fields.Single(f => f.Name == "Left").Id; var number = leaf.Fields.Single(f => f.Name == "Number").Id;
        static IrStatement.Define D(int id, IrType type, IrExpression expression) => new(new(id, type, 0, expression));
        var method = new IrMethod(new(input.Identity.DisplayName, input.ContentHash, typeof(ValueCopyProbe).GetMethod("Nested")!.MetadataToken, "Nested"),
            [IrType.Int32], IrType.Int32, [new(0, 0, [],
                [D(0, IrType.Int32, new IrExpression.Parameter(0)), D(1, item, new IrExpression.DefaultValue(item.Class!)),
                 D(2, item, new IrExpression.WithValueField(number, 1, 0)), D(3, pair, new IrExpression.DefaultValue(pair.Class!)),
                 D(4, pair, new IrExpression.WithValueField(left, 3, 2)), new IrStatement.StoreLocal(0, 0, 4),
                 D(5, pair, new IrExpression.Local(0)), D(6, item, new IrExpression.ValueField(left, 5)),
                 D(7, IrType.Int32, new IrExpression.Constant(7)), D(8, item, new IrExpression.WithValueField(number, 6, 7)),
                 D(9, pair, new IrExpression.WithValueField(left, 5, 8)), new IrStatement.StoreLocal(0, 0, 9),
                 D(10, item, new IrExpression.ValueField(left, 5)), D(11, IrType.Int32, new IrExpression.ValueField(number, 10)),
                 D(12, pair, new IrExpression.Local(0)), D(13, item, new IrExpression.ValueField(left, 12)),
                 D(14, IrType.Int32, new IrExpression.ValueField(number, 13)),
                 D(15, IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, 11, 14))], new IrReturn(1, 15))], [pair]);
        var entry = IrMethodId.From(method.Source);
        return (new(entry, [method], catalog), [new(entry, new(input.ContentHash, typeof(ValueCopyProbe).MetadataToken),
            ObjectMethodKind.Static, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, true)]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lowered_struct_copies_calls_and_parallel_backedges_match_dotnet(bool optimized)
    {
        int[] inputs = [int.MinValue, int.MaxValue, -31, -1, 0, 1, 17, 999];
        var source = new StringBuilder();
        var models = new[] { Model(false), Model(true), NestedModel() };
        for (int index = 0; index < models.Length; index++)
        {
            var (program, bindings) = models[index];
            var flow = ValueScalarLowering.Lower(program);
            Assert.DoesNotContain(flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values), v => v.Type.Kind == IrTypeKind.Value);
            var native = new ObjectIrProgram(flow, bindings); var artifact = ObjectCppEmitter.EmitProgram(native);
            source.Append(artifact.Text).Append(NativeObjectAdapters.Emit(native, artifact));
            string root = "cilica_m_" + flow.Entry.ContentHash + "_" + flow.Entry.Token.ToString("x8", CultureInfo.InvariantCulture);
            source.Append(CultureInfo.InvariantCulture, $$"""
                #include <cassert>
                [[gnu::noinline]] static void run_{{index}}() {
                    using namespace {{artifact.EntryPoint[..^4]}}_closure;
                    Instance heap; heap.collect_before_allocation = true; Context c(heap);
                """);
            foreach (int number in inputs)
            {
                int expected = index switch { 0 => ValueCopyProbe.Copy(number), 1 => ValueCopyProbe.Swap(number), _ => ValueCopyProbe.Nested(number) };
                source.Append(CultureInfo.InvariantCulture, $$"""
                    assert({{root}}(c, {{number}}) == {{expected}} && c.pending == 0);
                    assert(c.used == 0 && c.depth == 0 && c.top == nullptr && heap.count == 0 && heap.bytes == 0);
                    assert(collect(c).status == MarkStatus::complete);
                    """);
            }
            source.Append("}\n");
        }
        source.Append("int main() { run_0(); run_1(); run_2(); }\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-values");
        string cpp = Path.Combine(directory, "values.cpp"), executable = Path.Combine(directory, "values");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
             "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var result = await NativeProcess.Run(executable, [], directory);
        Assert.True(result.ExitCode == 0, result.Stdout + result.Stderr); Assert.Empty(result.Stderr);
    }
}
