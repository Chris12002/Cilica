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

public sealed class NativeDelegateTranslationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CIL_delegates_compose_with_boxes_generic_values_and_collectible_cycles(bool optimized)
    {
        var source = new StringBuilder(); var calls = new StringBuilder();
        foreach (string name in new[] { "Execute", "NullCapture", "NullOnly" })
        {
            var translated = DelegateTranslationTests.Translate(name);
            Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
            var program = translated.Program!; var flow = program.Flow.Flow;
            var artifact = ObjectCppEmitter.EmitDelegates(ValueScalarLowering.LowerDelegates(program));
            string symbol = "cilica_m_" + flow.Entry.ContentHash + "_" + flow.Entry.Token.ToString("x8", CultureInfo.InvariantCulture);
            var entry = flow.Methods.Single(m => IrMethodId.From(m.Source) == flow.Entry);
            var faultMethod = name == "Execute" ? flow.Methods.Single(m => m.Source.Name.EndsWith("::Fault", StringComparison.Ordinal)) : entry;
            int invokeOffset = entry.Blocks.SelectMany(b => b.Values).Single(v => v.Expression is IrExpression.CallDelegate).ILOffset;
            int failureOffset = faultMethod.Blocks.SelectMany(b => b.Values).Single(v => name == "Execute"
                ? v.Expression is IrExpression.Binary { Operation: Int32Operation.DivideSigned }
                : name == "NullCapture" ? v.Expression is IrExpression.NewDelegate : v.Expression is IrExpression.CallDelegate).ILOffset;
            source.Append(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
                #include <cassert>
                [[gnu::noinline]] static void run_{{name}}(std::int32_t number, std::int32_t mode, std::int32_t expected, std::uint32_t status) {
                    using namespace {{artifact.EntryPoint[..^4]}}_closure;
                    Instance heap; heap.object_limit = 16; heap.collect_before_allocation = true; Context c(heap);
                    LocationArgument arguments[2]; arguments[0].bits = std::bit_cast<std::uint32_t>(number); arguments[1].bits = std::bit_cast<std::uint32_t>(mode);
                    for (int i = 0; i < 3; ++i) {
                        auto result = {{symbol}}(c, arguments); assert(c.pending == status && (status != 0 || result == expected));
                        if (status != 0) {
                            assert(c.method == (status == 4 ? {{entry.Source.Token}}u : {{faultMethod.Source.Token}}u));
                            assert(c.il == (status == 4 ? {{invokeOffset}}u : {{failureOffset}}u));
                        }
                        assert(c.top == nullptr && c.depth == 0 && c.borrows == nullptr && c.used == 0);
                        c.pending = 0; assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                    }
                    std::int32_t inputs[2] = {number, mode}, result = 123;
                    std::uint32_t method = 0, offset = 0; const char* image = nullptr;
                    assert({{artifact.EntryPoint}}(&result, &method, &offset, &image, inputs, {{entry.Parameters.Length}}) == status);
                    assert(result == (status == 0 ? expected : 123));
                }
                """);
            foreach (int number in new[] { 0, -19, int.MinValue, int.MaxValue })
            foreach (int mode in name == "Execute" ? new[] { 0, 1, 2 } : new[] { 0 })
            {
                int expected = 0; uint status = 0;
                try { expected = name == "Execute" ? DelegateProbe.Execute(number, mode) : name == "NullCapture" ? DelegateProbe.NullCapture(number) : DelegateProbe.NullOnly(number); }
                catch (DivideByZeroException) { status = 1; }
                catch (NullReferenceException) { status = 4; }
                catch (ArgumentException) { status = 12; }
                calls.Append(CultureInfo.InvariantCulture, $"run_{name}({number}, {mode}, {expected}, {status}u);\n");
            }
        }
        source.Append("int main() {\n").Append(calls).Append("}\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-delegate-cil");
        string cpp = Path.Combine(directory, "delegates.cpp"), executable = Path.Combine(directory, "delegates");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
