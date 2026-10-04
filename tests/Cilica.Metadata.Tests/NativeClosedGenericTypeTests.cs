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

public sealed class NativeClosedGenericTypeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Closed_struct_layouts_keep_exact_box_identity_and_embedded_roots(bool optimized)
    {
        var translated = ClosedGenericTypeTests.Translate();
        Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
        var program = translated.Program!;
        var artifact = ObjectCppEmitter.EmitBoxes(ValueScalarLowering.LowerBoxes(program));
        var id = program.Flow.Flow.Entry;
        var entryMethod = program.Flow.Flow.Methods.Single(m => IrMethodId.From(m.Source) == id);
        int faultOffset = entryMethod.Blocks.SelectMany(b => b.Values).Single(v => v.Expression is IrExpression.UnboxValue).ILOffset;
        string entry = "cilica_m_" + id.ContentHash + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture);
        var source = new StringBuilder(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            [[gnu::noinline]] static void execute(std::int32_t number, std::int32_t mode, std::int32_t expected, std::uint32_t fault) {
                Instance heap; heap.collect_before_allocation = true; heap.object_limit = 16; Context c(heap);
                LocationArgument args[2]; args[0].bits = std::bit_cast<std::uint32_t>(number); args[1].bits = std::bit_cast<std::uint32_t>(mode);
                for (int repeat = 0; repeat < 3; ++repeat) {
                    auto result = {{entry}}(c, args); assert(c.pending == fault && (fault != 0 || result == expected));
                    if (fault != 0) assert(c.method == {{entryMethod.Source.Token}}u && c.il == {{faultOffset}}u);
                    assert(c.top == nullptr && c.depth == 0 && c.borrows == nullptr && c.used == 0);
                    c.pending = 0; assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                }
                std::int32_t inputs[] = {number, mode}, output = 123; std::uint32_t method = 0, offset = 0; const char* image = nullptr;
                assert({{artifact.EntryPoint}}(&output, &method, &offset, &image, inputs, 2) == fault && output == (fault == 0 ? expected : 123));
                if (fault != 0) assert(method == {{entryMethod.Source.Token}}u && offset == {{faultOffset}}u);
            }
            int main() {
            """);
        foreach (int number in new[] { 0, -19, int.MinValue, int.MaxValue })
        foreach (int mode in new[] { 0, 1, 2 })
        {
            int expected = 0; uint fault = 0;
            try { expected = ClosedGenericTypeProbe.Execute(number, mode); }
            catch (NullReferenceException) { fault = 4; } catch (InvalidCastException) { fault = 7; }
            source.Append(CultureInfo.InvariantCulture, $"execute({number}, {mode}, {expected}, {fault}u);\n");
        }
        source.Append("}\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-generic-types");
        string cpp = Path.Combine(directory, "generic.cpp"), executable = Path.Combine(directory, "generic");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
