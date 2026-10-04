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

public sealed class NativeValueReturnTests
{
    internal static (ManagedLocationIrProgram Program, ImmutableArray<ObjectMethodBinding> Bindings) Model()
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(ValueReturnProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result = ValueModelBuilder.BuildCatalog(resolver,
            [resolver.ResolveType(input, (TypeDefinitionHandle)MetadataTokens.EntityHandle(typeof(ValueLeaf).MetadataToken)).Value!], library);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics)); var catalog = result.Catalog!;
        var leaf = catalog.Types.Single(); var type = IrType.Value(ObjectTypeId.From(leaf.Source));
        var number = leaf.Fields.Single(f => f.Name == "Number").Id;
        IrMethod Method(string name, ImmutableArray<IrType> parameters, IrType returns, ImmutableArray<IrStatement> statements, int returned) =>
            new(new(input.Identity.DisplayName, input.ContentHash, typeof(ValueReturnProbe).GetMethod(name)!.MetadataToken, name), parameters, returns,
                [new(0, 0, [], statements, new IrReturn(1, returned))], []);
        var make = Method("Make", [IrType.Int32], type,
            [D(0, IrType.Int32, new IrExpression.Parameter(0)), D(1, type, new IrExpression.DefaultValue(type.Class!)),
             D(2, type, new IrExpression.WithValueField(number, 1, 0))], 2);
        var change = Method("Change", [type, IrType.Int32], type,
            [D(0, type, new IrExpression.Parameter(0)), D(1, IrType.Int32, new IrExpression.Parameter(1)),
             D(2, IrType.Int32, new IrExpression.ValueField(number, 0)), D(3, IrType.Int32, new IrExpression.Binary(Int32Operation.DivideSigned, 2, 1), true),
             D(4, type, new IrExpression.WithValueField(number, 0, 3))], 4);
        var echo = Method("Echo", [type], type, [D(0, type, new IrExpression.Parameter(0))], 0);
        var forward = Method("Forward", [type], type, [D(0, type, new IrExpression.Parameter(0)),
            D(1, type, new IrExpression.Call(IrMethodId.From(echo.Source), [0]), true)], 1);
        var entry = Method("Execute", [IrType.Int32, IrType.Int32], IrType.Int32,
            [D(0, IrType.Int32, new IrExpression.Parameter(0)), D(1, IrType.Int32, new IrExpression.Parameter(1)),
             D(2, type, new IrExpression.Call(IrMethodId.From(make.Source), [0]), true),
             D(3, type, new IrExpression.Call(IrMethodId.From(change.Source), [2, 1]), true),
             D(4, type, new IrExpression.Call(IrMethodId.From(forward.Source), [3]), true), D(5, IrType.Int32, new IrExpression.Constant(7)),
             D(6, type, new IrExpression.Call(IrMethodId.From(make.Source), [5]), true),
             D(7, IrType.Int32, new IrExpression.ValueField(number, 4)), D(8, IrType.Int32, new IrExpression.ValueField(number, 6)),
             D(9, IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, 7, 8))], 9);
        var methods = ImmutableArray.Create(entry, make, change, echo, forward);
        var owner = new ObjectTypeId(input.ContentHash, typeof(ValueReturnProbe).MetadataToken);
        return (new(new(IrMethodId.From(entry.Source), methods, catalog)), methods.Select(m => new ObjectMethodBinding(IrMethodId.From(m.Source),
            owner, ObjectMethodKind.Static, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, true)).ToImmutableArray());
    }

    private static IrStatement.Define D(int id, IrType type, IrExpression expression, bool fault = false) =>
        new(new(id, type, 0, expression), fault ? IrExceptionTarget.MethodExit : null);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Caller_owned_return_packets_preserve_values_references_faults_and_cleanup(bool optimized)
    {
        var (program, bindings) = Model(); var lowered = ValueScalarLowering.LowerLocations(program, bindings);
        var artifact = ObjectCppEmitter.EmitLocations(lowered);
        static string Symbol(IrMethodId method) => "cilica_m_" + method.ContentHash + "_" + method.Token.ToString("x8", CultureInfo.InvariantCulture);
        static string TypeSymbol(ObjectTypeId type) => "t_" + type.ContentHash + "_" + type.Token.ToString("x8", CultureInfo.InvariantCulture);
        string entry = Symbol(program.Flow.Entry), echo = Symbol(IrMethodId.From(program.Flow.Methods.Single(m => m.Source.Name == "Echo").Source));
        int changeToken = program.Flow.Methods.Single(m => m.Source.Name == "Change").Source.Token;
        string obj = TypeSymbol(program.Flow.Types.References.ObjectBase), str = TypeSymbol(program.Flow.Types.References.String!);
        var source = new StringBuilder(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            [[gnu::noinline]] static void execute(std::int32_t number, std::int32_t divisor, std::uint32_t fault, std::int32_t expected) {
                Instance heap; Context c(heap); LocationArgument arguments[2];
                arguments[0].bits = std::bit_cast<std::uint32_t>(number); arguments[1].bits = std::bit_cast<std::uint32_t>(divisor);
                const auto value = {{entry}}(c, arguments);
                assert(c.pending == fault && (fault != 0 || value == expected));
                assert(fault == 0 || c.method == {{changeToken}}u);
                assert(c.top == nullptr && c.borrows == nullptr && c.depth == 0 && c.used == 0 && heap.count == 0);
                std::int32_t inputs[] = {number, divisor}; std::int32_t output = 123; std::uint32_t method = 0, il = 0; const char* image = nullptr;
                assert({{artifact.EntryPoint}}(&output, &method, &il, &image, inputs, 2) == fault);
                assert(output == (fault == 0 ? expected : 123));
                assert(fault == 0 || method == {{changeToken}}u);
            }
            [[gnu::noinline]] static void reference_return() {
                Instance heap; Context c(heap); Object* slots[3]{}; std::uint32_t number = 99; Frame frame;
                RootScope roots(c, frame, slots, 3);
                static constexpr ReferencePredicate accepts[] = { {{obj}}_accepts, {{obj}}_accepts, {{str}}_accepts };
                BorrowScope borrow(c, frame, &number, 1, accepts);
                slots[0] = allocate<{{obj}}>(c, {{obj}}_descriptor); Object* original = slots[0]; assert(original);
                LocationArgument arguments[6]; arguments[0].bits = 17; arguments[1].reference = original;
                arguments[3].location = frame_location(c, borrow.frame.token, LocationOrigin::frame_integer, 0).value;
                arguments[4].location = frame_location(c, borrow.frame.token, LocationOrigin::frame_reference, 1).value;
                arguments[5].location = frame_location(c, borrow.frame.token, LocationOrigin::frame_reference, 2).value;
                {{echo}}(c, arguments);
                assert(c.pending == 0 && number == 17 && slots[1] == original && slots[2] == nullptr);
                slots[0] = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                number = 99; slots[1] = nullptr; slots[0] = original; arguments[5].location = {};
                {{echo}}(c, arguments);
                assert(c.pending == 6 && number == 99 && slots[1] == nullptr);
                assert(c.depth == 1 && c.borrows == &borrow.frame && c.used == 0);
                c.pending = 0; slots[0] = nullptr;
                assert(collect(c).unreachable == 1 && heap.count == 0);
            }
            [[gnu::noinline]] static std::size_t measure_return_storage() {
                Instance heap; Context c(heap); LocationArgument arguments[2]; arguments[1].bits = 1;
                assert({{entry}}(c, arguments) == 7 && c.pending == 0 && c.peak > 0);
                return c.peak;
            }
            [[gnu::noinline]] static void check_return_storage(std::size_t bytes, bool success) {
                Instance heap; Context c(heap, bytes); LocationArgument arguments[2]; arguments[1].bits = 1;
                const auto value = {{entry}}(c, arguments);
                assert(c.pending == (success ? 0u : 9u) && (!success || value == 7));
                assert(c.top == nullptr && c.borrows == nullptr && c.depth == 0 && c.used == 0);
            }
            int main() {
            """);
        foreach (int number in new[] { 0, -19, int.MinValue, int.MaxValue })
            foreach (int divisor in new[] { 1, 3, 0, -1 })
            {
                int expected = 0; uint status = 0;
                try { expected = ValueReturnProbe.Execute(number, divisor); }
                catch (DivideByZeroException) { status = 1; }
                catch (OverflowException) { status = 2; }
                source.Append(CultureInfo.InvariantCulture, $"execute({number}, {divisor}, {status}u, {expected});\n");
            }
        source.Append("reference_return();\nconst auto bytes = measure_return_storage();\ncheck_return_storage(bytes, true);\ncheck_return_storage(bytes - 1, false);\n}\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-value-returns");
        string cpp = Path.Combine(directory, "returns.cpp"), executable = Path.Combine(directory, "returns");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
             "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
