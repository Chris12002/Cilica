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

public sealed class NativeValueObjectTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CIL_class_value_programs_preserve_construction_roots_returns_and_faults(bool optimized)
    {
        var result = ValueObjectTranslationTests.Authored(); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!; var flow = program.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitLocations(ValueScalarLowering.LowerObjects(program));
        static string Symbol(IrMethodId id) => "cilica_m_" + id.ContentHash + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture);
        string TypeSymbol(Type type)
        {
            var id = ObjectTypeId.From(flow.Types.References.Types.Single(t => t.Source.Token == type.MetadataToken && t.Source.ContentHash == flow.Entry.ContentHash).Source);
            return "t_" + id.ContentHash + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture);
        }
        IrMethod Method(string name) => flow.Methods.Single(m => m.Source.Token == typeof(ValueObjectProbe).GetMethod(name)!.MetadataToken && m.Source.ContentHash == flow.Entry.ContentHash);
        var make = Method("Make"); var echo = Method("AllocateEcho");
        string entry = Symbol(flow.Entry), makeSymbol = Symbol(IrMethodId.From(make.Source)), echoSymbol = Symbol(IrMethodId.From(echo.Source));
        var nodeCtor = flow.Methods.Single(m => m.Source.Token == typeof(ValueObjectNode).GetConstructors().Single().MetadataToken && m.Source.ContentHash == flow.Entry.ContentHash);
        int division = nodeCtor.Blocks.SelectMany(b => b.Values).Single(v => v.Expression is IrExpression.Binary { Operation: Int32Operation.DivideSigned }).ILOffset;
        int allocation = nodeCtor.Blocks.SelectMany(b => b.Values).Single(v => v.Expression is IrExpression.NewObject).ILOffset;
        string node = TypeSymbol(typeof(ValueObjectNode)), child = TypeSymbol(typeof(ValueObjectChild)), payload = TypeSymbol(typeof(ValueObjectPayload));
        var slots = ValueLayoutPlanner.Build(flow.Types).Single(p => p.Type == make.ReturnType.Class).Slots;
        Assert.IsType<ObjectStorage.Reference>(slots[0].Storage); Assert.IsType<ObjectStorage.Int32>(slots[1].Storage);
        var source = new StringBuilder(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            static std::size_t allocations = 0;
            static void observe(Context& c) noexcept { ++allocations; assert(c.top != nullptr && valid_roots(c)); }
            [[gnu::noinline]] static void execute(std::int32_t number, std::int32_t divisor, std::uint32_t fault, std::int32_t expected) {
                Instance heap; heap.collect_before_allocation = true; heap.object_limit = 16; Context c(heap); c.observe = observe;
                LocationArgument arguments[2]; arguments[0].bits = std::bit_cast<std::uint32_t>(number); arguments[1].bits = std::bit_cast<std::uint32_t>(divisor);
                for (int i = 0; i < 3; ++i) {
                    allocations = 0; const auto output = {{entry}}(c, arguments);
                    assert(c.pending == fault && (fault != 0 || output == expected));
                    if (fault == 0) assert(allocations == 33);
                    else assert(c.method == {{nodeCtor.Source.Token}}u && c.il == {{division}}u);
                    assert(c.top == nullptr && c.borrows == nullptr && c.depth == 0 && c.used == 0);
                    c.pending = 0; assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                }
                std::int32_t inputs[] = {number, divisor}, output = 123;
                std::uint32_t method = 0, offset = 0; const char* image = nullptr;
                assert({{artifact.EntryPoint}}(&output, &method, &offset, &image, inputs, 2) == fault);
                assert(output == (fault == 0 ? expected : 123));
                assert(fault == 0 || (method == {{nodeCtor.Source.Token}}u && offset == {{division}}u));
            }
            [[gnu::noinline]] static void graph_return_and_heap_budget(int mode) {
                Instance heap; heap.collect_before_allocation = true; heap.object_limit = 8;
                if (mode != 0) heap.limit = 2 * sizeof({{child}}) + 4 * sizeof({{payload}}) - (mode == 2 ? 1 : 0);
                Context c(heap); Object* outputs[2]{}; std::uint32_t marker = 99; Frame frame;
                RootScope roots(c, frame, outputs, 2);
                static constexpr ReferencePredicate accepts[] = { {{node}}_accepts, {{node}}_accepts };
                BorrowScope borrow(c, frame, &marker, 1, accepts);
                LocationArgument make[4]; make[0].bits = 17; make[1].bits = 1;
                make[2].location = frame_location(c, borrow.frame.token, LocationOrigin::frame_reference, 0).value;
                make[3].location = frame_location(c, borrow.frame.token, LocationOrigin::frame_integer, 0).value;
                {{makeSymbol}}(c, make);
                assert(c.pending == (mode == 2 ? 5u : 0u) && marker == (mode == 2 ? 99u : 17u));
                assert((outputs[0] == nullptr) == (mode == 2));
                if (mode == 0) {
                    Object* original = outputs[0];
                    LocationArgument echo[5]; echo[0].reference = original; echo[1].bits = 17; echo[2].bits = 8;
                    echo[3].location = frame_location(c, borrow.frame.token, LocationOrigin::frame_reference, 1).value;
                    echo[4].location = make[3].location;
                    outputs[0] = nullptr;
                    // Only the callee parameter roots this six-object graph during allocation.
                    {{echoSymbol}}(c, echo);
                    assert(c.pending == 0 && outputs[1] == original && marker == 45);
                    assert(collect(c).status == MarkStatus::complete && heap.count == 6);
                    assert(heap.owns(original)); outputs[1] = nullptr;
                }
                assert(c.top == &frame && c.borrows == &borrow.frame && c.depth == 1 && c.used == 0);
                outputs[0] = nullptr; c.pending = 0;
                assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
            }
            [[gnu::noinline]] static void constructor_allocation_failure() {
                Instance heap; heap.collect_before_allocation = true; heap.object_limit = 1; Context c(heap);
                LocationArgument arguments[2]; arguments[1].bits = 1;
                (void){{entry}}(c, arguments);
                assert(c.pending == 5 && c.method == {{nodeCtor.Source.Token}}u && c.il == {{allocation}}u);
                assert(c.top == nullptr && c.borrows == nullptr && c.depth == 0 && c.used == 0);
                c.pending = 0; assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                heap.object_limit = 16;
                assert({{entry}}(c, arguments) == {{ValueObjectProbe.Execute(0, 1)}} && c.pending == 0);
                assert(c.top == nullptr && c.borrows == nullptr && c.depth == 0 && c.used == 0);
                assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
            }
            [[gnu::noinline]] static std::size_t arena(std::size_t bytes, bool success) {
                Instance heap; heap.collect_before_allocation = true; Context c(heap, bytes); LocationArgument arguments[2]; arguments[1].bits = 1;
                const auto output = {{entry}}(c, arguments);
                assert(c.pending == (success ? 0u : 9u) && (!success || output == {{ValueObjectProbe.Execute(0, 1)}}));
                assert(c.top == nullptr && c.borrows == nullptr && c.depth == 0 && c.used == 0);
                c.pending = 0; assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                return c.peak;
            }
            int main() {
                graph_return_and_heap_budget(0); graph_return_and_heap_budget(1); graph_return_and_heap_budget(2); constructor_allocation_failure();
                const auto peak = arena(1048576, true); assert(peak > 0); arena(peak, true); arena(peak - 1, false);
            """);
        foreach (int number in new[] { 0, 7, -19, int.MinValue, int.MaxValue })
            foreach (int divisor in new[] { 0, 1, 3, -1 })
            {
                int expected = 0; uint fault = 0;
                try { expected = ValueObjectProbe.Execute(number, divisor); }
                catch (DivideByZeroException) { fault = 1; }
                catch (OverflowException) { fault = 2; }
                source.Append(CultureInfo.InvariantCulture, $"execute({number}, {divisor}, {fault}u, {expected});\n");
            }
        source.Append("}\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-value-objects");
        string cpp = Path.Combine(directory, "objects.cpp"), executable = Path.Combine(directory, "objects");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
             "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
