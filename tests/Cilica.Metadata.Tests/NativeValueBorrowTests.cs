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

public sealed class NativeValueBorrowTests
{
    internal static (ManagedLocationIrProgram Program, ImmutableArray<ObjectMethodBinding> Bindings) Model()
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(ValueBorrowProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result = ValueModelBuilder.BuildCatalog(resolver,
            [resolver.ResolveType(input, (TypeDefinitionHandle)MetadataTokens.EntityHandle(typeof(ValuePair).MetadataToken)).Value!], library);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics)); var catalog = result.Catalog!;
        var pair = catalog.Types.Single(t => t.Source.Token == typeof(ValuePair).MetadataToken);
        var leaf = catalog.Types.Single(t => t.Source.Token == typeof(ValueLeaf).MetadataToken);
        var pairType = IrType.Value(ObjectTypeId.From(pair.Source)); var leafType = IrType.Value(ObjectTypeId.From(leaf.Source));
        var pairLocation = IrType.Location(pairType); var leafLocation = IrType.Location(leafType); var intLocation = IrType.Location(IrType.Int32);
        var readonlyPair = IrType.Location(pairType, true); var readonlyLeaf = IrType.Location(leafType, true); var readonlyInt = IrType.Location(IrType.Int32, true);
        var left = pair.Fields.Single(f => f.Name == "Left").Id; var right = pair.Fields.Single(f => f.Name == "Right").Id;
        var number = leaf.Fields.Single(f => f.Name == "Number").Id;
        IrMethod Method(string name, ImmutableArray<IrType> parameters, IrType returns, ImmutableArray<IrStatement> statements,
            ImmutableArray<IrType> locals, int returned = 0) => new(new(input.Identity.DisplayName, input.ContentHash,
                typeof(ValueBorrowProbe).GetMethod(name)!.MetadataToken, name), parameters, returns,
                [new(0, 0, [], statements, returns == IrType.Void ? new IrVoidReturn(1) : new IrReturn(1, returned))], locals);
        var swap = Method("Swap", [leafLocation, leafLocation], IrType.Void,
            [D(0, leafLocation, new IrExpression.Parameter(0)), D(1, leafLocation, new IrExpression.Parameter(1)),
             D(2, leafType, new IrExpression.LoadLocation(0), true), D(3, leafType, new IrExpression.LoadLocation(1), true),
             new IrStatement.StoreLocation(0, 0, 3), new IrStatement.StoreLocation(0, 1, 2)], []);
        var mutate = Method("MutateCopy", [pairType], IrType.Int32,
            [D(0, pairLocation, new IrExpression.AddressParameter(0)), D(1, leafLocation, new IrExpression.LocationField(left, 0)),
             D(2, intLocation, new IrExpression.LocationField(number, 1)), D(3, IrType.Int32, new IrExpression.Constant(99)),
             new IrStatement.StoreLocation(0, 2, 3), D(4, IrType.Int32, new IrExpression.LoadLocation(2), true)], [], 4);
        var read = Method("Read", [readonlyPair], IrType.Int32,
            [D(0, readonlyPair, new IrExpression.Parameter(0)), D(1, readonlyLeaf, new IrExpression.LocationField(left, 0)),
             D(2, readonlyInt, new IrExpression.LocationField(number, 1)), D(3, IrType.Int32, new IrExpression.LoadLocation(2), true)], [], 3);
        var entry = Method("Execute", [IrType.Int32], IrType.Int32,
            [D(0, IrType.Int32, new IrExpression.Parameter(0)), D(1, IrType.Int32, new IrExpression.Constant(7)),
             D(2, pairLocation, new IrExpression.AddressLocal(0)), new IrStatement.InitializeLocation(0, 2),
             D(3, leafLocation, new IrExpression.LocationField(left, 2)), D(4, intLocation, new IrExpression.LocationField(number, 3)),
             new IrStatement.StoreLocation(0, 4, 0), D(5, leafLocation, new IrExpression.LocationField(right, 2)),
             D(6, intLocation, new IrExpression.LocationField(number, 5)), new IrStatement.StoreLocation(0, 6, 1),
             new IrStatement.Invoke(0, IrMethodId.From(swap.Source), [3, 5]), D(7, pairType, new IrExpression.LoadLocation(2), true),
             new IrStatement.Invoke(0, IrMethodId.From(swap.Source), [3, 3]), new IrStatement.StoreLocation(0, 2, 7),
             D(8, IrType.Int32, new IrExpression.Call(IrMethodId.From(mutate.Source), [7]), true),
             D(9, readonlyPair, new IrExpression.ReadOnlyLocation(2)), D(10, IrType.Int32, new IrExpression.Call(IrMethodId.From(read.Source), [9]), true),
             D(11, IrType.Int32, new IrExpression.LoadLocation(6), true), D(12, IrType.Int32, new IrExpression.Constant(10000)),
             D(13, IrType.Int32, new IrExpression.Constant(100)), D(14, IrType.Int32, new IrExpression.Binary(Int32Operation.MultiplyWrapping, 10, 12)),
             D(15, IrType.Int32, new IrExpression.Binary(Int32Operation.MultiplyWrapping, 8, 13)),
             D(16, IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, 14, 15)),
             D(17, IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, 16, 11))], [pairType], 17);
        var methods = ImmutableArray.Create(entry, swap, mutate, read);
        var owner = new ObjectTypeId(input.ContentHash, typeof(ValueBorrowProbe).MetadataToken);
        var bindings = methods.Select(m => new ObjectMethodBinding(IrMethodId.From(m.Source), owner, ObjectMethodKind.Static,
            MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, true)).ToImmutableArray();
        return (new(new(IrMethodId.From(entry.Source), methods, catalog)), bindings);
    }

    internal static IrStatement.Define D(int id, IrType type, IrExpression expression, bool fault = false) =>
        new(new(id, type, 0, expression), fault ? IrExceptionTarget.MethodExit : null);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Expanded_mutable_values_preserve_snapshots_aliases_and_embedded_roots(bool optimized)
    {
        var (program, bindings) = Model(); var lowered = ValueScalarLowering.LowerLocations(program, bindings);
        var artifact = ObjectCppEmitter.EmitLocations(lowered);
        static string Symbol(IrMethodId method) => "cilica_m_" + method.ContentHash + "_" + method.Token.ToString("x8", CultureInfo.InvariantCulture);
        static string TypeSymbol(ObjectTypeId type) => "t_" + type.ContentHash + "_" + type.Token.ToString("x8", CultureInfo.InvariantCulture);
        string entry = Symbol(program.Flow.Entry), swap = Symbol(IrMethodId.From(program.Flow.Methods.Single(m => m.Source.Name == "Swap").Source));
        string obj = TypeSymbol(program.Flow.Types.References.ObjectBase), str = TypeSymbol(program.Flow.Types.References.String!);
        var source = new StringBuilder(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            [[gnu::noinline]] static void execute(std::int32_t input, std::int32_t expected) {
                Instance heap; Context c(heap); LocationArgument argument; argument.bits = std::bit_cast<std::uint32_t>(input);
                assert({{entry}}(c, &argument) == expected && c.pending == 0);
                assert(c.top == nullptr && c.borrows == nullptr && c.depth == 0 && c.used == 0 && heap.count == 0);
            }
            [[gnu::noinline]] static void reference_swap() {
                Instance heap; Context c(heap);
                Object* slots[4]{}; std::uint32_t numbers[] = {19u, 23u}; Frame frame;
                RootScope roots(c, frame, slots, 4);
                static constexpr ReferencePredicate accepts[] = { {{obj}}_accepts, {{str}}_accepts, {{obj}}_accepts, {{str}}_accepts };
                BorrowScope borrow(c, frame, numbers, 2, accepts);
                slots[0] = allocate<{{obj}}>(c, {{obj}}_descriptor);
                slots[2] = allocate<{{obj}}>(c, {{obj}}_descriptor);
                Object* first = slots[0]; Object* second = slots[2]; assert(first && second && first != second);
                LocationArgument arguments[6];
                arguments[0].location = frame_location(c, borrow.frame.token, LocationOrigin::frame_integer, 0).value;
                arguments[1].location = frame_location(c, borrow.frame.token, LocationOrigin::frame_reference, 0).value;
                arguments[2].location = frame_location(c, borrow.frame.token, LocationOrigin::frame_reference, 1).value;
                arguments[3].location = frame_location(c, borrow.frame.token, LocationOrigin::frame_integer, 1).value;
                arguments[4].location = frame_location(c, borrow.frame.token, LocationOrigin::frame_reference, 2).value;
                arguments[5].location = frame_location(c, borrow.frame.token, LocationOrigin::frame_reference, 3).value;
                {{swap}}(c, arguments);
                assert(c.pending == 0 && numbers[0] == 23 && numbers[1] == 19 && slots[0] == second && slots[2] == first);
                assert(collect(c).status == MarkStatus::complete && heap.count == 2);
                // All leaf capabilities are checked before a callee mutates any field.
                arguments[5].location = {};
                {{swap}}(c, arguments);
                assert(c.pending == 6 && numbers[0] == 23 && numbers[1] == 19 && slots[0] == second && slots[2] == first);
                assert(c.depth == 1 && c.borrows == &borrow.frame && c.used == 0);
                c.pending = 0; slots[0] = nullptr; slots[2] = nullptr;
                assert(collect(c).unreachable == 2 && heap.count == 0);
            }
            int main() {
            """);
        foreach (int input in new[] { 0, 7, -19, int.MinValue, int.MaxValue })
            source.Append(CultureInfo.InvariantCulture, $"execute({input}, {ValueBorrowProbe.Execute(input)});\n");
        source.Append("reference_swap();\n}\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-value-borrows");
        string cpp = Path.Combine(directory, "values.cpp"), executable = Path.Combine(directory, "values");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
             "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
