using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class LinqWhereTests
{
    private static readonly string[] Names = ["Deferred", "Clones", "Cardinality", "Combined", "Empty", "Fallback", "Faults", "ReferenceCurrent", "ResetAndNull", "FaultResume"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(LinqWhereProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 3)] [InlineData(true, 3)]
    public async Task Deferred_where_identity_state_callbacks_and_faults_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        var inputs = new[] { int.MinValue, -3, -1, 0, 1, 2, 3, 4, int.MaxValue };
        var pairs = (from a in inputs from b in new[] { -4, -3, -2, -1, 0, 1, 2, 3 } select (a, b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(LinqWhereProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray(), pairs, persistentRoots: true);
    }

    // Keep callback-heavy generated closures in separate compiler processes;
    // all original inputs and the 30-second cap remain unchanged.
    [Theory]
    [InlineData(false, 3)] [InlineData(true, 3)]
    [InlineData(false, 4)] [InlineData(true, 4)]
    [InlineData(false, 5)] [InlineData(true, 5)]
    [InlineData(false, 6)] [InlineData(true, 6)]
    [InlineData(false, 7)] [InlineData(true, 7)]
    [InlineData(false, 8)] [InlineData(true, 8)]
    public async Task Callback_closures_match_dotnet_individually(bool optimized, int probe)
    {
        var p = (await Programs.Value)[probe];
        var pairs = (from a in new[] { int.MinValue, -3, -1, 0, 1, 2, 3, 4, int.MaxValue }
                     from b in new[] { -4, -3, -2, -1, 0, 1, 2, 3 } select (a, b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [(Names[probe], p, typeof(LinqWhereProbe).GetMethod(Names[probe])!.CreateDelegate<Func<int, int, int>>())], pairs, persistentRoots: true);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Dispose_clears_current_but_keeps_source_and_predicate_until_query_dies(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(LinqWhereProbe), ["Tracked"]))[0];
        var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(string name) { var method = flow.Methods.Single(m => m.Source.Name == "Cilica.Fixtures.Execution.LinqWhereProbe::" + name); return $"cilica_m_{method.Source.ContentHash}_{method.Source.Token:x8}"; }
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0);
                Object* iterator = nullptr; Frame frame; RootScope roots(c, frame, &iterator, 1);
                iterator = {{Symbol("MakeTracked")}}(c, nullptr);
                assert(c.pending == 0 && iterator != nullptr);
                assert(collect(c).status == MarkStatus::complete && heap.count == 7);
                LocationArgument args[1]; args[0].reference = iterator;
                {{Symbol("StopTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == 6);
                {{Symbol("StopTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == 6);
                iterator = nullptr; args[0].reference = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                assert(c.used == 0 && c.borrows == nullptr);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("where-gc"), cpp = Path.Combine(directory, "where.cpp"), executable = Path.Combine(directory, "where");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Public_noncapturing_predicate_cache_has_exact_instance_owned_roots(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(CompatibilityGapProbe), ["Probe_LinqWhere"]))[0];
        var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string symbol = $"cilica_m_{flow.Entry.ContentHash}_{flow.Entry.Token:x8}";
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                Instance other; other.collect_before_allocation = true; Context d(other);
                LocationArgument args[2]; args[0].bits = 1; args[1].bits = 2;
                assert({{symbol}}(c, args) == 2 && c.pending == 0);
                assert({{symbol}}(d, args) == 2 && d.pending == 0);
                // Closure discovery also declares caches that this input never populates.
                std::size_t occupied = 0;
                assert(collect(c).status == MarkStatus::complete && heap.count == 3);
                assert(collect(d).status == MarkStatus::complete && other.count == 3);
                for (std::size_t i = 0; i < static_field_count; ++i) {
                    if (heap.statics[i].reference == nullptr) { assert(other.statics[i].reference == nullptr); continue; }
                    ++occupied;
                    assert(other.statics[i].reference != nullptr && heap.statics[i].reference != other.statics[i].reference);
                    heap.statics[i].reference = nullptr;
                }
                assert(occupied == 2);
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                assert(collect(d).status == MarkStatus::complete && other.count == 3);
                assert(c.used == 0 && c.top == nullptr && c.borrows == nullptr);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("where-cache"), cpp = Path.Combine(directory, "cache.cpp"), executable = Path.Combine(directory, "cache");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
