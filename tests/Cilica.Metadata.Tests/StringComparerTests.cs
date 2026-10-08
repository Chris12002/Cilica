using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class StringComparerTests
{
    private static readonly string[] Names = ["Direct", "Typed", "Equality", "Identity", "Integers", "Values", "NullReceiver"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(StringComparerProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)] [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Ordinal_comparer_and_exact_generic_contracts_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        var pairs = group == 0 ? (from a in Enumerable.Range(0, 20) from b in Enumerable.Range(0, 20) select (a, b)).ToArray() : null;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(StringComparerProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray(), pairs, persistentRoots: true);
    }
    [Theory]
    [InlineData("UnsupportedComparerHash")] [InlineData("UnsupportedObjectCompare")]
    [InlineData("UnsupportedIgnoreCase")] [InlineData("UnsupportedObjectEquality")]
    public async Task Unimplemented_comparer_shapes_remain_diagnostic(string name)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(StringComparerProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(StringComparerProbe).GetMethod(name)!.MetadataToken);
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains(name == "UnsupportedIgnoreCase" ? "No implementation method matches" : "no implemented managed body", StringComparison.Ordinal));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Typed_ordinal_hash_preserves_equal_text_and_null_faults(bool optimized)
    {
        var program=(await CoreLibraryBuildTests.TranslateFixtures(typeof(StringComparerProbe),["HashProperties"]))[0];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("HashProperties",program,StringComparerProbe.HashProperties)],expectedStaticRoots:1);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Ordinal_singleton_has_one_instance_owned_root(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(StringComparerProbe), ["Singleton"]))[0]; var flow = program.Flow.Flow.Flow;
        Assert.Single(flow.Types.References.Statics);
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string symbol = $"cilica_m_{flow.Entry.ContentHash}_{flow.Entry.Token:x8}";
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                Instance other; other.collect_before_allocation = true; Context d(other);
                assert(prepare_exceptions(c) == 0 && prepare_exceptions(d) == 0);
                assert(collect(c).status == MarkStatus::complete && heap.count == 1 && heap.head == heap.emergency_exception);
                assert(collect(d).status == MarkStatus::complete && other.count == 1 && other.head == other.emergency_exception);
                LocationArgument args[2]; args[0].bits = 65; args[1].bits = 66;
                for (int i = 0; i < 3; i++) {
                    assert({{symbol}}(c, args) == 17 && c.pending == 0);
                    assert({{symbol}}(d, args) == 17 && d.pending == 0);
                    assert(collect(c).status == MarkStatus::complete && heap.count == 2);
                    assert(collect(d).status == MarkStatus::complete && other.count == 2);
                    assert(heap.statics[0].reference != nullptr && other.statics[0].reference != nullptr);
                    assert(heap.statics[0].reference != other.statics[0].reference);
                    assert(heap.owns(heap.statics[0].reference) && !other.owns(heap.statics[0].reference));
                    assert(other.owns(other.statics[0].reference) && !heap.owns(other.statics[0].reference));
                    assert(c.used == 0 && d.used == 0 && c.borrows == nullptr && d.borrows == nullptr);
                }
                heap.statics[0].reference = nullptr; other.statics[0].reference = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1 && heap.head == heap.emergency_exception);
                assert(collect(d).status == MarkStatus::complete && other.count == 1 && other.head == other.emergency_exception);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("ordinal-comparer"), cpp = Path.Combine(directory, "comparer.cpp"), executable = Path.Combine(directory, "comparer");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
