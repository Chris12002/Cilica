using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class DictionaryCapacityTests
{
    private static readonly string[] Names = ["Empty", "Integers", "References", "Collisions", "Negative", "Enumeration"];
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    public async Task Capacity_construction_and_composed_operations_match_dotnet(bool optimized, int group)
    {
        var names = Names.Skip(group * 2).Take(2).ToArray();
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(DictionaryCapacityProbe), names);
        var pairs = (from a in new[] { 0, 1, 3, 4, 7, 8, 11, 12, 17, 18, 37, 38, 89 }
            from b in new[] { int.MinValue, -1, 0, 1, 2, 31, int.MaxValue } select (a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, names.Select((name,i) =>
            (name, programs[i], typeof(DictionaryCapacityProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).ToArray(), pairs);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Failed_capacity_allocations_release_roots_and_allow_recovery(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(DictionaryCapacityProbe), "Empty"))[0];
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        var entry = program.Flow.Flow.Flow.Entry;
        string symbol = $"cilica_m_{entry.ContentHash}_{entry.Token:x8}";
        string source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                for (std::size_t quota : {0u, 64u, 256u, 512u, 1024u}) {
                    Instance heap; heap.collect_before_allocation = true; Context c(heap);
                    assert(prepare_exceptions(c) == 0); const auto permanent = heap.bytes;
                    heap.limit = permanent + quota;
                    LocationArgument args[2]; args[0].bits = 197u; args[1].bits = 0u;
                    (void){{symbol}}(c, args);
                    assert(c.pending != 0 && c.used == 0 && c.top == nullptr && c.borrows == nullptr && c.depth == 0);
                    c.pending = 0; c.exception = nullptr;
                    assert(collect(c).status == MarkStatus::complete && heap.count == 1 && heap.bytes == permanent);
                    heap.limit = 1048576; args[0].bits = 7u;
                    assert({{symbol}}(c, args) == 0 && c.pending == 0);
                    assert(collect(c).status == MarkStatus::complete && heap.count == 1 && heap.bytes == permanent);
                }
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("dictionary-capacity"), cpp = Path.Combine(directory, "capacity.cpp"), exe = Path.Combine(directory, "capacity");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
