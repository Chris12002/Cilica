using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class LinqIndexedWhereTests
{
    private static readonly string[] Names = ["State", "Clones", "Faults", "Terminals", "Mutation", "References", "Values", "EmptyAndNull", "FaultReuse"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(LinqIndexedWhereProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    [InlineData(false, 3)] [InlineData(true, 3)]
    [InlineData(false, 4)] [InlineData(true, 4)]
    [InlineData(false, 5)] [InlineData(true, 5)]
    [InlineData(false, 6)] [InlineData(true, 6)]
    [InlineData(false, 7)] [InlineData(true, 7)]
    [InlineData(false, 8)] [InlineData(true, 8)]
    public async Task Indexed_where_matches_dotnet(bool optimized, int probe)
    {
        var program = (await Programs.Value)[probe];
        var pairs = (from a in new[] { int.MinValue, -1, 0, 1, 2, 3, 4, 5, 6, 7, int.MaxValue }
                     from b in new[] { -1, 0, 1, int.MinValue, int.MaxValue } select (a, b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [(Names[probe], program, typeof(LinqIndexedWhereProbe).GetMethod(Names[probe])!.CreateDelegate<Func<int, int, int>>())], pairs);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Dispose_releases_source_cursor_but_keeps_current_until_query_dies(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(LinqIndexedWhereProbe), ["Tracked"]))[0];
        var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(string name) { var method = flow.Methods.Single(m => m.Source.Name == "Cilica.Fixtures.Execution.LinqIndexedWhereProbe::" + name); return $"cilica_m_{method.Source.ContentHash}_{method.Source.Token:x8}"; }
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0);
                Object* iterator = nullptr; Frame frame; RootScope roots(c, frame, &iterator, 1);
                iterator = {{Symbol("MakeTracked")}}(c, nullptr);
                assert(c.pending == 0 && iterator != nullptr);
                assert(collect(c).status == MarkStatus::complete && heap.count == 8);
                LocationArgument args[1]; args[0].reference = iterator;
                {{Symbol("StopTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == 7);
                {{Symbol("StopTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == 7);
                iterator = nullptr; args[0].reference = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                assert(c.used == 0 && c.borrows == nullptr);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("indexed-where-gc"), cpp = Path.Combine(directory, "where.cpp"), executable = Path.Combine(directory, "where");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }

}
