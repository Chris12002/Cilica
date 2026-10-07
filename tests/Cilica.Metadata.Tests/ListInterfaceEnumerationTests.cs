using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ListInterfaceEnumerationTests
{
    private static readonly string[] Names = ["Integers", "Identity", "Current", "Mutation", "References", "Values", "DefaultAndEmptyValue", "NullAndFinally", "EmptyCache"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ListInterfaceEnumerationProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Interface_cursors_match_dotnet(bool optimized, int batch)
    {
        var programs = await Programs.Value;
        var probes = Names.Select((name, i) => (name, programs[i], typeof(ListInterfaceEnumerationProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, probes.Skip(batch * 3).Take(batch == 2 ? 2 : 3).ToArray());
        if (batch == 2) await IntegerObjectTranslationTests.VerifyTranslated(optimized, probes.Skip(8).ToArray(), expectedStaticRoots: 1);
    }
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Cursor_roots_retain_only_the_expected_live_graph(bool optimized, int kind)
    {
        string entry = kind == 0 ? "Tracked" : kind == 1 ? "EmptyTracked" : "CurrentTracked";
        string create = kind == 0 ? "CreateTracked" : kind == 1 ? "CreateEmptyTracked" : "CreateCurrentTracked";
        string observe = kind == 0 ? "EndTracked" : kind == 1 ? "EndEmptyTracked" : "ReadTracked";
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(ListInterfaceEnumerationProbe), [entry]))[0];
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(string name)
        {
            var source = program.Flow.Flow.Flow.Methods.Single(m => m.Source.Name == "Cilica.Fixtures.Execution.ListInterfaceEnumerationProbe::" + name).Source;
            return $"cilica_m_{source.ContentHash}_{source.Token:x8}";
        }
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0);
                Object* cursor = nullptr; Frame frame;
                { RootScope roots(c, frame, &cursor, 1);
                cursor = {{Symbol(create)}}(c, nullptr);
                assert(c.pending == 0 && cursor != nullptr);
                assert(collect(c).status == MarkStatus::complete && heap.count == {{(kind == 1 ? 3 : 6)}}u);
                LocationArgument args[1]; args[0].reference = cursor;
                {{(kind == 2 ? "assert(" + Symbol(observe) + "(c, args) == 17);" : Symbol(observe) + "(c, args);")}}
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == {{(kind == 1 ? 3 : 6)}}u);
                cursor = nullptr; args[0].reference = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == {{(kind == 1 ? 3 : 2)}}u);
                assert(c.used == 0 && c.borrows == nullptr && c.depth == 1 && c.top == &frame);
                }
                assert(c.used == 0 && c.borrows == nullptr && c.depth == 0 && c.top == nullptr);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("list-cursor-roots");
        string cpp = Path.Combine(directory, "probe.cpp"), executable = Path.Combine(directory, "probe");
        File.WriteAllText(cpp, source);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
             "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }

}
