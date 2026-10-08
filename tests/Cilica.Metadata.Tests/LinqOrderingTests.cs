using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class LinqOrderingTests
{
    private static readonly string[] Names = ["Stable", "Multiple", "Repeated", "TraceSort", "TraceChain", "Faults", "SelectorFault", "Cursor", "Terminals", "ChainAny", "Inline", "CustomThen", "Validation", "Inconsistent", "SourceFault"];
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    [InlineData(false, 4)] [InlineData(true, 4)]
    public async Task Ordering_preserves_results_callbacks_and_failures(bool optimized, int group)
    {
        string[] names = Names.Skip(group * 3).Take(3).ToArray();
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(OrderingProbe), names);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, names.Select((name, i) =>
            (name, programs[i], typeof(OrderingProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).ToArray(),
            (from a in new[] { int.MinValue, -1, 0, 1, 2, 3, 4, 15, 16, 17, 31, 32, 63, int.MaxValue }
             from b in new[] { int.MinValue, -1, 0, 1, 2, 3, 7, 15, int.MaxValue } select (a, b)).ToArray(), persistentRoots: true);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Sort_fault_text_matches_every_code_unit(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(OrderingProbe), ["FaultCodeUnit"]))[0];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("FaultCodeUnit", program, OrderingProbe.FaultCodeUnit)],
            (from a in new[] { -1, 1 } from b in Enumerable.Range(-1, 300) select (a, b)).ToArray(), persistentRoots: true);
    }
    [Theory]
    [InlineData("UnsupportedUnsigned", "Delegate requires")]
    public async Task Unimplemented_key_profiles_remain_diagnostic(string name, string diagnostic)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(OrderingProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(OrderingProbe).GetMethod(name)!.MetadataToken);
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains(diagnostic, StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(false, "UnsupportedWide")] [InlineData(true, "UnsupportedWide")]
    [InlineData(false, "UnsupportedString")] [InlineData(true, "UnsupportedString")]
    public async Task Previously_rejected_wide_and_explicit_ordinal_cardinality_probes_execute(bool optimized, string name)
    {
        var program=(await CoreLibraryBuildTests.TranslateFixtures(typeof(OrderingProbe), [name]))[0];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [(name, program, typeof(OrderingProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())], expectedStaticRoots:3);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Dispose_releases_sorted_buffers_and_current_while_preserving_the_source(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(OrderingProbe), ["Tracked"]))[0];
        var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(string name) { var method = flow.Methods.Single(m => m.Source.Name == "Cilica.Fixtures.Execution.OrderingProbe::" + name); return $"cilica_m_{method.Source.ContentHash}_{method.Source.Token:x8}"; }
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0);
                Object* iterator = nullptr; Frame frame; RootScope roots(c, frame, &iterator, 1);
                iterator = {{Symbol("MakeTracked")}}(c, nullptr);
                assert(c.pending == 0 && iterator != nullptr);
                assert(collect(c).status == MarkStatus::complete && heap.count == 11);
                LocationArgument args[1]; args[0].reference = iterator;
                {{Symbol("StopTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == 8);
                {{Symbol("StopTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == 8);
                iterator = nullptr; args[0].reference = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                assert(c.used == 0 && c.borrows == nullptr);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("order-gc"), cpp = Path.Combine(directory, "order.cpp"), executable = Path.Combine(directory, "order");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Adversarial_inputs_execute_heap_fallback_with_exact_callback_order(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(OrderingProbe), ["Adversarial"]))[0];
        var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        var heapSort = flow.Methods.Single(m => m.Source.Name == "Cilica.Runtime.MapSort::HeapSort");
        string heapSymbol = $"cilica_m_{heapSort.Source.ContentHash}_{heapSort.Source.Token:x8}";
        string entry = $"cilica_m_{flow.Entry.ContentHash}_{flow.Entry.Token:x8}";
        string header = "[[maybe_unused]] static void " + heapSymbol + "([[maybe_unused]] Context& context, [[maybe_unused]] const LocationArgument* arguments) noexcept {";
        Assert.Contains(header, artifact.Text, StringComparison.Ordinal);
        // Test-only instrumentation observes entry, without changing operands/control flow.
        string instrumented = artifact.Text.Replace(header, "static int observed_heap_calls = 0;\n" + header + "\n++observed_heap_calls;", StringComparison.Ordinal);
        var source = instrumented + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            static void run(std::uint32_t first, std::int32_t expected, int heaps) {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                LocationArgument args[2]; args[0].bits = first; args[1].bits = 0;
                observed_heap_calls = 0;
                assert({{entry}}(c, args) == expected && c.pending == 0);
                assert(observed_heap_calls == heaps);
                assert(collect(c).status == MarkStatus::complete && c.used == 0 && c.borrows == nullptr);
            }
            int main() {
                run(0, {{OrderingProbe.Adversarial(0, 0)}}, 5);
                run(0xffffffffu, {{OrderingProbe.Adversarial(-1, 0)}}, 3);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("order-heap"), cpp = Path.Combine(directory, "heap.cpp"), executable = Path.Combine(directory, "heap");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
