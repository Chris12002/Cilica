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

public sealed class NativeBoxedCilTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CIL_boxing_preserves_graphs_copies_borrow_mutation_and_fault_origins(bool optimized)
    {
        var translated = BoxedValueTranslationTests.Authored(); Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
        var program = translated.Program!; var flow = program.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitBoxes(ValueScalarLowering.LowerBoxes(program));
        var copy = flow.Methods.Single(m => m.Source.Token == typeof(BoxedValueProbe).GetMethod("Copy")!.MetadataToken);
        int unboxOffset = copy.Blocks.SelectMany(b => b.Values).Single(v => v.Expression is IrExpression.UnboxValue).ILOffset;
        int boxOffset = flow.Methods.Single(m => IrMethodId.From(m.Source) == flow.Entry).Blocks.SelectMany(b => b.Values).First(v => v.Expression is IrExpression.BoxValue).ILOffset;
        string entry = "cilica_m_" + flow.Entry.ContentHash + "_" + flow.Entry.Token.ToString("x8", CultureInfo.InvariantCulture);
        var reduced = BoxedValueTranslationTests.Reduced("valid"); Assert.True(reduced.Success, string.Join("; ", reduced.Diagnostics));
        var borrowed = ObjectCppEmitter.EmitBoxes(ValueScalarLowering.LowerBoxes(reduced.Program!));
        var reducedId = reduced.Program!.Flow.Flow.Entry;
        string reducedEntry = "cilica_m_" + reducedId.ContentHash + "_" + reducedId.Token.ToString("x8", CultureInfo.InvariantCulture);
        var source = new StringBuilder(artifact.Text).Append(borrowed.Text).Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            [[gnu::noinline]] static void execute(std::int32_t number, std::int32_t mode, std::uint32_t fault, std::int32_t expected) {
                Instance heap; heap.collect_before_allocation = true; heap.object_limit = 16; Context c(heap);
                LocationArgument args[2]; args[0].bits = std::bit_cast<std::uint32_t>(number); args[1].bits = std::bit_cast<std::uint32_t>(mode);
                for (int repeat = 0; repeat < 3; ++repeat) {
                    const auto output = {{entry}}(c, args);
                    assert(c.pending == fault && (fault != 0 || output == expected));
                    if (fault != 0) assert(c.method == {{copy.Source.Token}}u && c.il == {{unboxOffset}}u);
                    assert(c.top == nullptr && c.depth == 0 && c.borrows == nullptr && c.used == 0);
                    c.pending = 0; assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                }
                std::int32_t inputs[] = {number, mode}, output = 123; std::uint32_t method = 0, offset = 0; const char* image = nullptr;
                assert({{artifact.EntryPoint}}(&output, &method, &offset, &image, inputs, 2) == fault);
                assert(output == (fault == 0 ? expected : 123));
                if (fault != 0) assert(method == {{copy.Source.Token}}u && offset == {{unboxOffset}}u);
            }
            [[gnu::noinline]] static void allocation_failure_and_reuse() {
                Instance heap; heap.object_limit = 6; heap.collect_before_allocation = true; Context c(heap); LocationArgument args[2];
                (void){{entry}}(c, args);
                assert(c.pending == 5 && c.method == {{flow.Entry.Token}}u && c.il == {{boxOffset}}u);
                assert(c.top == nullptr && c.depth == 0 && c.borrows == nullptr && c.used == 0);
                c.pending = 0; assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                heap.object_limit = 16; assert({{entry}}(c, args) == {{BoxedValueProbe.Execute(0, 0)}} && c.pending == 0);
                assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
            }
            [[gnu::noinline]] static void mutate_borrowed_CIL_payload() {
                namespace b = {{borrowed.EntryPoint[..^4]}}_closure;
                b::Instance heap; heap.object_limit = 2; heap.collect_before_allocation = true; b::Context c(heap);
                assert(b::{{reducedEntry}}(c, nullptr) == 17 && c.pending == 0);
                assert(c.top == nullptr && c.depth == 0 && c.borrows == nullptr && c.used == 0);
                assert(b::collect(c).status == b::MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
            }
            int main() { allocation_failure_and_reuse(); mutate_borrowed_CIL_payload();
            """);
        foreach (int number in new[] { 0, -19, int.MinValue, int.MaxValue })
            foreach (int mode in new[] { 0, 1, 2 })
            {
                int expected = 0; uint fault = 0;
                try { expected = BoxedValueProbe.Execute(number, mode); }
                catch (NullReferenceException) { fault = 4; }
                catch (InvalidCastException) { fault = 7; }
                source.Append(CultureInfo.InvariantCulture, $"execute({number}, {mode}, {fault}u, {expected});\n");
            }
        source.Append("}\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-boxed-cil");
        string cpp = Path.Combine(directory, "boxes.cpp"), executable = Path.Combine(directory, "boxes");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
