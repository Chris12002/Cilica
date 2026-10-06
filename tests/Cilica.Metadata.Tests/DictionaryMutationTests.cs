using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class DictionaryMutationTests
{
    private static readonly string[] Names = ["Integers", "Clear", "Strings", "Values", "Collisions", "CallbackFaults", "Nulls", "Interfaces", "Tracked", "TrackedValues"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(DictionaryMutationProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Removal_clear_and_reuse_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(DictionaryMutationProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Value_cleanup_matches_dotnet(bool optimized)
    {
        var program = (await Programs.Value)[9];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("TrackedValues", program, DictionaryMutationProbe.TrackedValues)]);
    }
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task Removed_references_are_collectible_while_the_dictionary_stays_rooted(bool optimized, bool valueElements)
    {
        var program = (await Programs.Value)[valueElements ? 9 : 8];
        var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(string name)
        {
            var method = flow.Methods.Single(m => m.Source.Name == "Cilica.Fixtures.Execution.DictionaryMutationProbe::" + name + (valueElements ? "Values" : ""));
            return $"cilica_m_{method.Source.ContentHash}_{method.Source.Token:x8}";
        }
        string source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0);
                Object* list = nullptr; Frame frame; RootScope roots(c, frame, &list, 1);
                list = {{Symbol("CreateTracked")}}(c, nullptr);
                assert(c.pending == 0 && list != nullptr && collect(c).status == MarkStatus::complete);
                auto before = heap.count;
                LocationArgument args[1]; args[0].reference = list;
                {{Symbol("RemoveTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before - 1);
                {{Symbol("ClearTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before - 4);
                {{Symbol("ClearTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before - 4);
                list = nullptr; args[0].reference = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1 && heap.emergency_exception != nullptr);
                assert(c.used == 0 && c.borrows == nullptr);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("dictionary-gc"), cpp = Path.Combine(directory, "list.cpp"), executable = Path.Combine(directory, "list");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
