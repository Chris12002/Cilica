using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class SplitCacheTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateSeparatorProbe), ["CacheBoundary", "CacheQuota"]));

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Cached_prefix_and_rescanned_suffix_preserve_text_precedence_trimming_and_identity(bool optimized)
    {
        var program = (await Programs.Value)[0];
        int[] counts = [0, 1, 31, 32, 33, 63, 64, 65, 80, 129];
        var pairs = (from count in counts from packed in Enumerable.Range(0, 16) select (count, packed)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("CacheBoundary", program, CandidateSeparatorProbe.CacheBoundary)], pairs);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Cache_and_result_allocation_failures_release_temporaries_and_allow_recovery(bool optimized)
    {
        var program = (await Programs.Value)[1];
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string symbol = "cilica_m_" + artifact.Source.ContentHash + "_" + artifact.Source.Token.ToString("x8", CultureInfo.InvariantCulture);
        CandidateSeparatorProbe.CacheQuota(0, 0);
        int expected = CandidateSeparatorProbe.CacheQuota(1, 0);
        CandidateSeparatorProbe.CacheQuota(-1, 0);
        string source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context initialize(heap); LocationArgument args[2]{};
                assert({{symbol}}(initialize, args) == 1 && initialize.pending == 0);
                args[0].bits = 1; assert({{symbol}}(initialize, args) == {{expected}} && initialize.pending == 0);
                args[0].bits = 0xffffffffu; assert({{symbol}}(initialize, args) == 0 && initialize.pending == 0);
                assert(collect(initialize).status == MarkStatus::complete); const auto permanent = heap.count;
                args[0].bits = 0; assert({{symbol}}(initialize, args) == 1 && initialize.pending == 0);
                assert(collect(initialize).status == MarkStatus::complete); const auto retained = heap.count;
                args[0].bits = 1;
                for (std::size_t allowance : {0u, 1u, 2u, 3u}) {
                    heap.object_limit = retained + allowance; Context failure(heap); (void){{symbol}}(failure, args);
                    assert(failure.pending == 5 && failure.top == nullptr && failure.borrows == nullptr && failure.used == 0 && failure.depth == 0);
                    heap.object_limit = 4096;
                    assert(collect(initialize).status == MarkStatus::complete && heap.count == retained);
                    Context recovery(heap); assert({{symbol}}(recovery, args) == {{expected}} && recovery.pending == 0);
                    assert(recovery.top == nullptr && recovery.borrows == nullptr && recovery.used == 0 && recovery.depth == 0);
                    assert(collect(recovery).status == MarkStatus::complete && heap.count == retained);
                }
                // The 65-match source uses the maximum 128-Int32 cache payload.
                heap.limit = heap.bytes + sizeof(ArrayObject) + 128 * sizeof(std::uint32_t) - 1;
                Context bytes(heap); (void){{symbol}}(bytes, args);
                assert(bytes.pending == 5 && bytes.top == nullptr && bytes.borrows == nullptr && bytes.used == 0);
                heap.limit = 1048576;
                assert(collect(initialize).status == MarkStatus::complete && heap.count == retained);
                Context recovery(heap); assert({{symbol}}(recovery, args) == {{expected}} && recovery.pending == 0);
                args[0].bits = 0xffffffffu; assert({{symbol}}(recovery, args) == 0 && recovery.pending == 0);
                assert(collect(recovery).status == MarkStatus::complete && heap.count == permanent);
            }
            """;
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("split-cache-quota");
        string cpp = Path.Combine(dir, "probe.cpp"), exe = Path.Combine(dir, "probe"); File.WriteAllText(cpp, source);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", optimized ? "-O2" : "-O0", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror",
             "-DCILICA_VERIFY_INTERNAL_REFERENCES=1", "-DCILICA_VERIFY_BORROW_LIFETIMES=1", "-DCILICA_VERIFY_ALLOCATION_ROOTS=1",
             "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], dir);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], dir); Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr);
    }

    [Fact]
    public async Task Cached_split_executes_across_separate_native_modules()
    {
        var module = ObjectCppEmitter.EmitExceptionModule(ValueScalarLowering.LowerExceptions((await Programs.Value)[0]), 8);
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("split-cache-module");
        foreach (var file in module.Sources.Prepend(module.Header)) File.WriteAllText(Path.Combine(dir, file.Name), file.Text);
        string main = Path.Combine(dir, "main.cpp"), entry = "cilica_m_" + module.Source.ContentHash + "_" + module.Source.Token.ToString("x8", CultureInfo.InvariantCulture);
        File.WriteAllText(main, $$"""
            #include "{{module.Header.Name}}"
            #include <cassert>
            using namespace {{module.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap); LocationArgument args[2]{};
                args[0].bits = 65; args[1].bits = 3;
                assert({{entry}}(c, args) == {{CandidateSeparatorProbe.CacheBoundary(65, 3)}} && c.pending == 0);
                assert(c.depth == 0 && c.used == 0 && c.top == nullptr && c.borrows == nullptr);
                assert(collect(c).status == MarkStatus::complete);
            }
            """);
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++"; var objects = new List<string>();
        foreach (string cpp in module.Sources.Select(file => Path.Combine(dir, file.Name)).Append(main))
        {
            string obj = cpp + ".o";
            var build = await NativeProcess.Run(compiler,
                ["-std=c++20", "-O2", "-DCILICA_TYPED_INTERNALS=1", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror",
                 "-fsanitize=address,undefined", "-fno-sanitize-recover=all", "-c", cpp, "-o", obj], dir);
            Assert.True(build.ExitCode == 0, build.Stderr); objects.Add(obj);
        }
        string exe = Path.Combine(dir, "probe");
        var link = await NativeProcess.Run(compiler, ["-fsanitize=address,undefined", .. objects, "-o", exe], dir);
        Assert.True(link.ExitCode == 0, link.Stderr);
        var run = await NativeProcess.Run(exe, [], dir); Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr);
    }
}
