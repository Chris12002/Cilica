using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeValueArrayTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CIL_values_preserve_live_arrays_during_collection_borrows_and_returns(bool optimized)
    {
        var result = ValueArrayTranslationTests.Authored(); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!; var flow = program.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitLocations(ValueScalarLowering.LowerArrays(program));
        static string Symbol(IrMethodId id) => "cilica_m_" + id.ContentHash + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture);
        IrMethod Method(string name) => flow.Methods.Single(m => m.Source.Token == typeof(ValueArrayProbe).GetMethod(name)!.MetadataToken);
        var make = Method("Make"); var echo = Method("AllocateEcho");
        string makeSymbol = Symbol(IrMethodId.From(make.Source)), echoSymbol = Symbol(IrMethodId.From(echo.Source));
        string entry = Symbol(flow.Entry);
        var vector = flow.Types.References.Arrays.Single();
        string vectorSymbol = "t_" + vector.ContentHash + "_" + vector.Token.ToString("x8", CultureInfo.InvariantCulture) + "_vector_i4";
        var plan = ValueLayoutPlanner.Build(flow.Types).Single(p => p.Type == make.ReturnType.Class);
        Assert.Equal(vector, Assert.IsType<ObjectStorage.Reference>(plan.Slots[0].Storage).Type);
        Assert.IsType<ObjectStorage.Int32>(plan.Slots[1].Storage);
        int newOffset = make.Blocks.SelectMany(b => b.Values).Single(v => v.Expression is IrExpression.NewArray).ILOffset;
        int storeOffset = make.Blocks.SelectMany(b => b.Statements).OfType<IrStatement.StoreElement>().Single().ILOffset;
        var pressure = Method("Pressure");
        int pressureOffset = pressure.Blocks.SelectMany(b => b.Values).Single(v => v.Expression is IrExpression.NewArray).ILOffset;
        var source = new StringBuilder(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            static std::size_t allocations = 0;
            static void observe(Context& c) noexcept { ++allocations; assert(c.top != nullptr && valid_roots(c)); }
            [[gnu::noinline]] static void execute(std::int32_t number, std::int32_t length, std::uint32_t fault, std::int32_t expected) {
                Instance heap; heap.collect_before_allocation = true; heap.object_limit = 8; Context c(heap); c.observe = observe;
                LocationArgument arguments[2]; arguments[0].bits = std::bit_cast<std::uint32_t>(number); arguments[1].bits = std::bit_cast<std::uint32_t>(length);
                for (int i = 0; i < 3; ++i) {
                    allocations = 0; const auto output = {{entry}}(c, arguments);
                    assert(c.pending == fault && (fault != 0 || output == expected));
                    if (fault == 0) assert(allocations == 20);
                    else assert(c.method == {{make.Source.Token}}u && c.il == (fault == 2 ? {{newOffset}}u : {{storeOffset}}u));
                    assert(c.top == nullptr && c.borrows == nullptr && c.depth == 0 && c.used == 0);
                    c.pending = 0;
                    assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                }
                std::int32_t inputs[] = {number, length}, output = 123;
                std::uint32_t method = 0, offset = 0; const char* image = nullptr;
                assert({{artifact.EntryPoint}}(&output, &method, &offset, &image, inputs, 2) == fault);
                assert(output == (fault == 0 ? expected : 123));
            }
            [[gnu::noinline]] static void returned_roots_and_parameter_roots() {
                Instance heap; heap.collect_before_allocation = true; heap.object_limit = 4; Context c(heap);
                Object* slots[2]{}; std::uint32_t marker = 0; Frame frame;
                RootScope roots(c, frame, slots, 2);
                static constexpr ReferencePredicate accepts[] = { {{vectorSymbol}}_accepts, {{vectorSymbol}}_accepts };
                BorrowScope borrow(c, frame, &marker, 1, accepts);
                LocationArgument make[4]; make[0].bits = 17; make[1].bits = 3;
                make[2].location = frame_location(c, borrow.frame.token, LocationOrigin::frame_reference, 0).value;
                make[3].location = frame_location(c, borrow.frame.token, LocationOrigin::frame_integer, 0).value;
                {{makeSymbol}}(c, make);
                assert(c.pending == 0 && slots[0] != nullptr && marker == 17);
                Object* original = slots[0];
                LocationArgument echo[5]; echo[0].reference = original; echo[1].bits = 17; echo[2].bits = 8;
                echo[3].location = frame_location(c, borrow.frame.token, LocationOrigin::frame_reference, 1).value;
                echo[4].location = make[3].location;
                // The callee parameter is now the only GC root until it publishes its return.
                slots[0] = nullptr;
                {{echoSymbol}}(c, echo);
                assert(c.pending == 0 && slots[1] == original && marker == 45);
                assert(c.top == &frame && c.borrows == &borrow.frame && c.depth == 1 && c.used == 0);
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                assert(heap.owns(original) && array_length(original) == 3 && load_integer_element(original, 0) == 17);
                slots[1] = nullptr;
                assert(collect(c).unreachable == 1 && heap.count == 0 && heap.bytes == 0);
            }
            [[gnu::noinline]] static void heap_budget(bool exact) {
                Instance heap; heap.collect_before_allocation = true; heap.limit = sizeof(ArrayObject) + 3 * sizeof(std::uint32_t) - (exact ? 0 : 1);
                Context c(heap); Object* output = nullptr; std::uint32_t marker = 99; Frame frame;
                RootScope roots(c, frame, &output, 1); static constexpr ReferencePredicate accepts[] = { {{vectorSymbol}}_accepts };
                BorrowScope borrow(c, frame, &marker, 1, accepts);
                LocationArgument arguments[4]; arguments[0].bits = 17; arguments[1].bits = 3;
                arguments[2].location = frame_location(c, borrow.frame.token, LocationOrigin::frame_reference, 0).value;
                arguments[3].location = frame_location(c, borrow.frame.token, LocationOrigin::frame_integer, 0).value;
                {{makeSymbol}}(c, arguments);
                assert(c.pending == (exact ? 0u : 5u) && marker == (exact ? 17u : 99u) && (output != nullptr) == exact);
                assert(c.used == 0 && c.top == &frame && c.borrows == &borrow.frame && c.depth == 1);
                output = nullptr; c.pending = 0; assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
            }
            [[gnu::noinline]] static std::size_t arena(std::size_t bytes, bool success) {
                Instance heap; heap.collect_before_allocation = true; Context c(heap, bytes); LocationArgument arguments[2]; arguments[1].bits = 3;
                const auto output = {{entry}}(c, arguments);
                assert(c.pending == (success ? 0u : 9u) && (!success || output == {{ValueArrayProbe.Execute(0, 3)}}));
                assert(c.top == nullptr && c.borrows == nullptr && c.depth == 0 && c.used == 0);
                c.pending = 0; assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                return c.peak;
            }
            [[gnu::noinline]] static void nested_allocation_failure() {
                Instance heap; heap.collect_before_allocation = true; heap.object_limit = 1; Context c(heap);
                LocationArgument arguments[2]; arguments[1].bits = 3;
                (void){{entry}}(c, arguments);
                assert(c.pending == 5 && c.method == {{pressure.Source.Token}}u && c.il == {{pressureOffset}}u);
                assert(c.top == nullptr && c.borrows == nullptr && c.depth == 0 && c.used == 0);
                c.pending = 0; assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                heap.object_limit = 8;
                assert({{entry}}(c, arguments) == {{ValueArrayProbe.Execute(0, 3)}} && c.pending == 0);
                assert(c.top == nullptr && c.borrows == nullptr && c.depth == 0 && c.used == 0);
                assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
            }
            int main() {
                returned_roots_and_parameter_roots(); heap_budget(true); heap_budget(false); nested_allocation_failure();
                const auto peak = arena(1048576, true); assert(peak > 0); arena(peak, true); arena(peak - 1, false);
            """);
        foreach (int number in new[] { 0, 7, -19, int.MinValue, int.MaxValue })
            foreach (int length in new[] { -1, 0, 1, 3 })
            {
                int expected = 0; uint fault = 0;
                try { expected = ValueArrayProbe.Execute(number, length); }
                catch (OverflowException) { fault = 2; }
                catch (IndexOutOfRangeException) { fault = 10; }
                source.Append(CultureInfo.InvariantCulture, $"execute({number}, {length}, {fault}u, {expected});\n");
            }
        source.Append("}\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-value-arrays");
        string cpp = Path.Combine(directory, "arrays.cpp"), executable = Path.Combine(directory, "arrays");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
             "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
