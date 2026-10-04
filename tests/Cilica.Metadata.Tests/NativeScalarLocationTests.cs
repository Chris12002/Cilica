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
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeScalarLocationTests
{
    internal static ScalarLocationIrProgram Model()
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(LocationProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var translation = ObjectTranslator.Translate(resolver, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(LocationProbe).GetMethod("Build")!.MetadataToken), library);
        Assert.True(translation.Success, string.Join("; ", translation.Diagnostics)); var original = translation.Program!;
        var entry = original.Flow.Methods.Single(m => IrMethodId.From(m.Source) == original.Flow.Entry);
        var owner = original.Bindings.Single(b => b.Method == original.Flow.Entry).Owner;
        var node = original.Flow.Types.Types.Single(t => t.Source.Token == typeof(LocationNode).MetadataToken && t.Source.ContentHash == input.ContentHash);
        var parent = original.Flow.Types.Types.Single(t => t.Source.Token == typeof(LocationBase).MetadataToken && t.Source.ContentHash == input.ContentHash);
        var nodeType = IrType.Reference(ObjectTypeId.From(node.Source)); var intLocation = IrType.Location(IrType.Int32);
        var nodeLocation = IrType.Location(nodeType);
        var number = parent.Fields.Single(f => f.Name == "Number").Id; var initial = parent.Fields.Single(f => f.Name == "Initial").Id;
        var constructor = original.Bindings.Single(b => b.Owner == nodeType.Class && b.Kind == ObjectMethodKind.Constructor).Method;
        IrMethod Method(string name, ImmutableArray<IrType> parameters, IrType result, ImmutableArray<IrStatement> statements,
            ImmutableArray<IrType> locals, int returned = 0) => new(entry.Source with { Token = typeof(LocationProbe).GetMethod(name)!.MetadataToken, Name = name },
                parameters, result, [new(0, 0, [], statements, result == IrType.Void ? new IrVoidReturn(1) : new IrReturn(1, returned))], locals);
        var mutate = Method("Mutate", [intLocation], IrType.Void,
            [D(0, intLocation, new IrExpression.Parameter(0)), D(1, IrType.Int32, new IrExpression.Constant(0)),
             D(2, nodeType, new IrExpression.NewObject(constructor, [1]), true), D(3, IrType.Int32, new IrExpression.LoadLocation(0), true),
             D(4, IrType.Int32, new IrExpression.Constant(1)), D(5, IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, 3, 4)),
             new IrStatement.StoreLocation(0, 0, 5)], []);
        var forward = Method("Reborrow", [intLocation], IrType.Void,
            [D(0, intLocation, new IrExpression.Parameter(0)), new IrStatement.Invoke(0, IrMethodId.From(mutate.Source), [0])], []);
        var readonlyLocation = IrType.Location(IrType.Int32, true);
        var read = Method("Read", [readonlyLocation], IrType.Int32,
            [D(0, readonlyLocation, new IrExpression.Parameter(0)), D(1, IrType.Int32, new IrExpression.LoadLocation(0), true)], [], 1);
        var replace = Method("Replace", [nodeLocation, IrType.Int32], IrType.Void,
            [D(0, nodeLocation, new IrExpression.Parameter(0)), D(1, IrType.Int32, new IrExpression.Parameter(1)),
             D(2, IrType.Int32, new IrExpression.Constant(7)), D(3, IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, 1, 2)),
             D(4, nodeType, new IrExpression.NewObject(constructor, [3]), true), new IrStatement.StoreLocation(0, 0, 4),
             D(5, nodeType, new IrExpression.NewObject(constructor, [1]), true)], []);
        entry = Method("Execute", [IrType.Int32], IrType.Int32,
            [D(0, IrType.Int32, new IrExpression.Parameter(0)), D(1, intLocation, new IrExpression.AddressLocal(0)),
             new IrStatement.InitializeLocation(0, 1), new IrStatement.StoreLocation(0, 1, 0),
             D(2, intLocation, new IrExpression.AddressParameter(0)), new IrStatement.Invoke(0, IrMethodId.From(forward.Source), [2]),
             D(3, IrType.Int32, new IrExpression.Parameter(0)), D(4, nodeType, new IrExpression.NewObject(constructor, [3]), true),
             new IrStatement.StoreLocal(0, 1, 4), D(5, nodeLocation, new IrExpression.AddressLocal(1)),
             new IrStatement.Invoke(0, IrMethodId.From(replace.Source), [5, 3]), D(6, nodeType, new IrExpression.LoadLocation(5), true),
             D(7, intLocation, new IrExpression.AddressField(number, 6), true), new IrStatement.Invoke(0, IrMethodId.From(mutate.Source), [7]),
             D(8, IrType.Location(IrType.Int32, true), new IrExpression.AddressField(initial, 6), true),
             D(9, IrType.Int32, new IrExpression.LoadLocation(7), true), D(10, IrType.Int32, new IrExpression.Call(IrMethodId.From(read.Source), [8]), true),
             D(11, IrType.Int32, new IrExpression.LoadLocation(1), true), D(12, IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, 11, 9)),
             D(13, IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, 12, 10))], [IrType.Int32, nodeType], 13);
        var added = ImmutableArray.Create(entry, mutate, forward, replace, read);
        var methods = original.Flow.Methods.Where(m => IrMethodId.From(m.Source) != original.Flow.Entry).Concat(added).ToImmutableArray();
        var bindings = original.Bindings.Where(b => b.Method != original.Flow.Entry).Concat(added.Select(m => new ObjectMethodBinding(
            IrMethodId.From(m.Source), owner, ObjectMethodKind.Static, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, true))).ToImmutableArray();
        return new(new(new(IrMethodId.From(entry.Source), methods, original.Flow.Types), bindings));
    }

    internal static IrStatement.Define D(int id, IrType type, IrExpression expression, bool fault = false) =>
        new(new(id, type, 0, expression), fault ? IrExceptionTarget.MethodExit : null);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Authored_scalar_borrows_match_dotnet_and_survive_allocating_callees(bool optimized)
    {
        var program = Model(); var artifact = ObjectCppEmitter.EmitLocations(program);
        var methods = program.Flow.Flow.Methods;
        static string Symbol(IrMethod method) => "cilica_m_" + method.Source.ContentHash + "_" + method.Source.Token.ToString("x8", CultureInfo.InvariantCulture);
        string entry = Symbol(methods.Single(m => m.Source.Name == "Execute"));
        string mutate = Symbol(methods.Single(m => m.Source.Name == "Mutate"));
        var node = program.Flow.Flow.Types.Types.Single(t => t.Source.Name.EndsWith("LocationNode", StringComparison.Ordinal));
        string nodeSymbol = "t_" + node.Source.ContentHash + "_" + node.Source.Token.ToString("x8", CultureInfo.InvariantCulture);
        var source = new StringBuilder(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            [[gnu::noinline]] static void execute(std::int32_t input, std::int32_t expected) {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                LocationArgument argument; argument.bits = std::bit_cast<std::uint32_t>(input);
                for (int i = 0; i < 20; ++i) {
                    assert({{entry}}(c, &argument) == expected && c.pending == 0);
                    assert(c.top == nullptr && c.borrows == nullptr && c.depth == 0 && c.used == 0);
                    assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                }
            }
            [[gnu::noinline]] static void private_borrow_checks() {
                Instance heap; Context c(heap);
                Object* owner = allocate<{{nodeSymbol}}>(c, {{nodeSymbol}}_descriptor);
                LocationArgument argument; argument.location = heap_location(c, owner, &{{nodeSymbol}}_descriptor, 0).value;
                heap.collect_before_allocation = true;
                // The callee publishes the carrier owner before its first allocation.
                {{mutate}}(c, &argument);
                std::uint32_t value = 0;
                assert(c.pending == 0 && load_location_integer(c, argument.location, value) == LocationStatus::complete && value == 1);
                argument.location.read_only = true;
                {{mutate}}(c, &argument);
                assert(c.pending == 6 && c.top == nullptr && c.borrows == nullptr && c.used == 0);
                c.pending = 0; argument.location = {};
                {{mutate}}(c, &argument);
                assert(c.pending == 6 && c.top == nullptr && c.borrows == nullptr && c.used == 0);
                c.pending = 0;
                assert(collect(c).status == MarkStatus::complete && heap.count == 0);
                heap.object_limit = 0;
                (void){{entry}}(c, &argument);
                assert(c.pending == 5 && c.method == {{methods.Single(m => m.Source.Name == "Mutate").Source.Token}}u);
                assert(c.top == nullptr && c.borrows == nullptr && c.depth == 0 && c.used == 0);
                c.pending = 0; heap.object_limit = 65536;
                // Exhaustion must fail the lending frame without leaking execution storage.
                next_borrow_token.store(std::numeric_limits<std::uint64_t>::max());
                argument.bits = 1; (void){{entry}}(c, &argument);
                assert(c.pending == 9 && c.top == nullptr && c.borrows == nullptr && c.depth == 0 && c.used == 0);
            }
            int main() {
            """);
        foreach (int input in new[] { 0, 7, -19, int.MinValue, int.MaxValue })
            source.Append(CultureInfo.InvariantCulture, $"execute({input}, {LocationProbe.Execute(input)});\n");
        source.Append("private_borrow_checks();\n}\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-scalar-locations");
        string cpp = Path.Combine(directory, "borrow.cpp"), executable = Path.Combine(directory, "borrow");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
             "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
