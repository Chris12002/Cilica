using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class IntegerObjectTranslationTests
{
    private static ManagedExceptionTranslationResult Translate(string name, bool enabled = true)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(WideObjects).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var root = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(WideObjects).GetMethod(name)!.MetadataToken);
        return enabled ? ValueTranslator.TranslateIntegerObjects(resolver, input, root, library)
            : ValueTranslator.TranslateExceptions(resolver, input, root, library);
    }

    [Theory]
    [InlineData("Heap")] [InlineData("Values")] [InlineData("Exceptions")] [InlineData("Unsigned")]
    public void Wide_CIL_uses_explicit_catalog_and_leaves_legacy_profile_bounded(string name)
    {
        var result = Translate(name); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.True(result.Program!.Flow.Flow.Flow.Types.References.Integer64);
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program));
        Assert.False(Translate(name, enabled: false).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CIL_wide_heap_values_borrows_generics_and_exceptions_match_dotnet(bool optimized)
    {
        (string Name, Func<int, int, int> Oracle)[] probes = [
            (nameof(WideObjects.Heap), WideObjects.Heap), (nameof(WideObjects.Values), WideObjects.Values),
            (nameof(WideObjects.Exceptions), WideObjects.Exceptions), (nameof(WideObjects.Unsigned), WideObjects.Unsigned)];
        var source = new StringBuilder(); var calls = new StringBuilder();
        foreach (var (name, oracle) in probes)
        {
            var translated = Translate(name); Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
            var flow = translated.Program!.Flow.Flow.Flow;
            var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(translated.Program));
            string symbol = "cilica_m_" + flow.Entry.ContentHash + "_" + flow.Entry.Token.ToString("x8", CultureInfo.InvariantCulture);
            source.Append(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
                #include <cassert>
                #include <cstdio>
                [[gnu::noinline]] static void run_{{name}}(std::int32_t first, std::int32_t second, std::int32_t expected) {
                    using namespace {{artifact.EntryPoint[..^4]}}_closure;
                    Instance heap; heap.collect_before_allocation = true; Context c(heap);
                    LocationArgument args[2]; args[0].bits = std::bit_cast<std::uint32_t>(first); args[1].bits = std::bit_cast<std::uint32_t>(second);
                    for (int repeat = 0; repeat < 3; ++repeat) {
                        const auto result = {{symbol}}(c, args);
                        if (c.pending != 0 || result != expected) std::fprintf(stderr, "{{name}} %d/%d: status %u result %d expected %d\n", first, second, c.pending, result, expected);
                        assert(c.pending == 0 && result == expected && c.exception == nullptr);
                        assert(c.used == 0 && c.top == nullptr && c.borrows == nullptr && c.depth == 0);
                        assert(collect(c).status == MarkStatus::complete);
                        std::size_t permanent = 1;
                        for (auto* literal = heap.literals; literal; literal = literal->literal_next) ++permanent;
                        for (auto* resource = heap.exception_resources; resource; resource = resource->literal_next) ++permanent;
                        assert(heap.count == permanent);
                    }
                    const auto peak = c.peak; Context exact(heap, peak);
                    assert({{symbol}}(exact, args) == expected && exact.pending == 0 && exact.used == 0);
                    Context short_frame(heap, peak - 1); (void){{symbol}}(short_frame, args);
                    assert(short_frame.pending == 9 && short_frame.used == 0 && short_frame.top == nullptr && short_frame.borrows == nullptr);
                }
                """);
            foreach (int a in new[] { 0, 1, -1, 127, 32768, int.MinValue, int.MaxValue })
                foreach (int b in new[] { 0, 1, 2, 3, -1, int.MinValue, int.MaxValue })
                    calls.Append(CultureInfo.InvariantCulture, $"run_{name}({a}, {b}, {oracle(a, b)});\n");
        }
        source.Append("int main() {\n").Append(calls).Append("}\n");
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("wide-cil"), cpp = Path.Combine(dir, "wide.cpp"), exe = Path.Combine(dir, "wide");
        File.WriteAllText(cpp, source.ToString());
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], dir);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], dir); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
