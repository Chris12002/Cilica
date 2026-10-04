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

public sealed class NativeExceptionTranslationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CIL_exception_semantics_match_dotnet_with_collection_and_frame_budget_checks(bool optimized)
    {
        (string Name, Func<int, int> Oracle)[] probes = [
            ("TypedCatch", ManagedExceptionProbe.TypedCatch), ("Cleanup", ManagedExceptionProbe.Cleanup),
            ("TypeFilter", ManagedExceptionProbe.TypeFilter), ("Rethrow", ManagedExceptionProbe.Rethrow),
            ("NestedCleanup", ManagedExceptionProbe.NestedCleanup), ("Replacement", ManagedExceptionProbe.Replacement),
            ("Composed", ManagedExceptionProbe.Composed), ("Message", ManagedExceptionProbe.Message),
            ("OrderedCatches", ManagedExceptionProbe.OrderedCatches), ("RuntimeFaults", ManagedExceptionProbe.RuntimeFaults),
            ("AllocationFailure", ManagedExceptionProbe.AllocationFailure), ("GenericCastMessage", ManagedExceptionProbe.GenericCastMessage)];
        var source = new StringBuilder(); var calls = new StringBuilder();
        foreach (var (name, oracle) in probes)
        {
            var translated = ManagedExceptionTranslationTests.Translate(name);
            Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
            var program = translated.Program!; var flow = program.Flow.Flow.Flow;
            var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
            string symbol = "cilica_m_" + flow.Entry.ContentHash + "_" + flow.Entry.Token.ToString("x8", CultureInfo.InvariantCulture);
            source.Append(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
                #include <cassert>
                #include <cstdio>
                [[gnu::noinline]] static void run_{{name}}(std::int32_t number, std::int32_t expected) {
                    using namespace {{artifact.EntryPoint[..^4]}}_closure;
                    Instance heap; heap.object_limit = 100; heap.collect_before_allocation = true; Context c(heap);
                    LocationArgument arguments[1]; arguments[0].bits = std::bit_cast<std::uint32_t>(number);
                    for (int repeat = 0; repeat < 3; ++repeat) {
                        auto result = {{symbol}}(c, arguments);
                        if (c.pending != 0 || result != expected) std::fprintf(stderr, "{{name}} input %d: status %u result %d expected %d\n", number, c.pending, result, expected);
                        assert(c.pending == 0 && result == expected && c.exception == nullptr);
                        assert(c.top == nullptr && c.depth == 0 && c.borrows == nullptr && c.used == 0);
                        assert(collect(c).status == MarkStatus::complete);
                        std::size_t permanent = 1;
                        for (auto* literal = heap.literals; literal; literal = literal->literal_next) ++permanent;
                        assert(heap.count == permanent);
                    }
                    const auto peak = c.peak;
                    Context exact(heap, peak); assert({{symbol}}(exact, arguments) == expected && exact.pending == 0 && exact.used == 0);
                    Context short_frame(heap, peak - 1); (void){{symbol}}(short_frame, arguments);
                    assert(short_frame.pending == 9 && short_frame.exception == nullptr && short_frame.used == 0 && short_frame.top == nullptr && short_frame.borrows == nullptr);
                    std::int32_t result = 123; std::uint32_t method = 0, offset = 0; const char* image = nullptr;
                    assert({{artifact.EntryPoint}}(&result, &method, &offset, &image, &number, 1) == 0 && result == expected);
                }
                """);
            if (name == "AllocationFailure")
            {
                source.Append(CultureInfo.InvariantCulture, $$"""
                    [[gnu::noinline]] static void emergency_capacity() {
                        using namespace {{artifact.EntryPoint[..^4]}}_closure;
                        Instance heap; heap.object_limit = 1; heap.collect_before_allocation = true; Context c(heap);
                        LocationArgument arguments[1]; arguments[0].bits = 3;
                        assert({{symbol}}(c, arguments) == 77 && c.pending == 0 && heap.count == 1);
                        assert(c.exception == nullptr && c.used == 0 && c.top == nullptr);
                        heap.object_limit = 2;
                        assert({{symbol}}(c, arguments) == 3 && c.pending == 0);
                        assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                    }
                    """);
                calls.Append("emergency_capacity();\n");
            }
            if (name == "GenericCastMessage") Assert.Equal(1, oracle(0));
            foreach (int number in new[] { 0, 1, 2, 3, 4, -19, int.MinValue, int.MaxValue })
                calls.Append(CultureInfo.InvariantCulture, $"run_{name}({number}, {oracle(number)});\n");
        }
        {
            var translated = ManagedExceptionTranslationTests.Translate("Unhandled");
            Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
            var flow = translated.Program!.Flow.Flow.Flow;
            var failed = flow.Methods.Single(m => m.Source.Name.EndsWith("::Fault", StringComparison.Ordinal));
            int il = failed.Blocks.SelectMany(b => b.Values).Single(v => v.Expression is IrExpression.Binary { Operation: Int32Operation.DivideSigned }).ILOffset;
            var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(translated.Program));
            string symbol = "cilica_m_" + flow.Entry.ContentHash + "_" + flow.Entry.Token.ToString("x8", CultureInfo.InvariantCulture);
            source.Append(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
                #include <cassert>
                [[gnu::noinline]] static void unhandled_origin() {
                    using namespace {{artifact.EntryPoint[..^4]}}_closure;
                    Instance heap; heap.collect_before_allocation = true; Context c(heap);
                    LocationArgument arguments[1]; arguments[0].bits = 0;
                    (void){{symbol}}(c, arguments);
                    assert(c.pending == 1 && c.exception != nullptr && c.exception->type == runtime_exception(1));
                    assert(c.method == {{failed.Source.Token}}u && c.il == {{il}}u);
                    assert(c.top == nullptr && c.used == 0 && c.depth == 0 && c.borrows == nullptr);
                    assert(collect(c).status == MarkStatus::complete && heap.count == 2);
                    c.exception = nullptr; c.pending = 0;
                    assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                    arguments[0].bits = 2; assert({{symbol}}(c, arguments) == 50 && c.pending == 0);
                    std::int32_t number = 0, result = 123; std::uint32_t method = 0, offset = 0; const char* image = nullptr;
                    assert({{artifact.EntryPoint}}(&result, &method, &offset, &image, &number, 1) == 1);
                    assert(result == 123 && method == {{failed.Source.Token}}u && offset == {{il}}u && image != nullptr);
                }
                """);
            calls.Append("unhandled_origin();\n");
        }
        source.Append("int main() {\n").Append(calls).Append("}\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-exception-cil");
        string cpp = Path.Combine(directory, "exceptions.cpp"), executable = Path.Combine(directory, "exceptions");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
