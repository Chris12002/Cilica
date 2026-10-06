using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ArrayReadOnlyTests
{
    private static readonly string[] Names = ["Live", "Empty", "Reference", "Value", "Faults", "Covariant"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ArrayReadOnlyProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Live_views_empty_identity_and_faults_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ArrayReadOnlyProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray(), persistentRoots: true);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Empty_views_are_instance_owned_traced_and_released(bool optimized)
    {
        var program = (await Programs.Value)[1];
        var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string symbol = $"cilica_m_{flow.Entry.ContentHash}_{flow.Entry.Token:x8}";
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                Instance other; other.collect_before_allocation = true; Context d(other);
                LocationArgument args[2]; args[0].bits = 0; args[1].bits = 0;
                assert({{symbol}}(c, args) == 6 && c.pending == 0);
                assert({{symbol}}(d, args) == 6 && d.pending == 0);
                assert(static_field_count == 2);
                assert(collect(c).status == MarkStatus::complete && heap.count == 5);
                assert(collect(d).status == MarkStatus::complete && other.count == 5);
                for (std::size_t i = 0; i < static_field_count; ++i) {
                    assert(heap.statics[i].reference != nullptr && other.statics[i].reference != nullptr);
                    assert(heap.statics[i].reference != other.statics[i].reference);
                }
                assert({{symbol}}(c, args) == 6 && c.pending == 0);
                assert(collect(c).status == MarkStatus::complete && heap.count == 5);
                for (std::size_t i = 0; i < static_field_count; ++i) heap.statics[i].reference = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 3);
                assert(collect(d).status == MarkStatus::complete && other.count == 5);
                assert(c.used == 0 && c.top == nullptr && c.borrows == nullptr);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("readonly-array-gc"), cpp = Path.Combine(directory, "view.cpp"), executable = Path.Combine(directory, "view");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
