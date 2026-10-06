using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class DictionaryViewEnumerationTests
{
    private static readonly string[] Names = ["Order", "RemoveAndClear", "OverwriteAndAdd", "CopiesAndDefaults", "DefaultFaults", "ReferencesAndValues", "TrackedKeys", "TrackedValues"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(DictionaryViewEnumerationProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Concrete_view_enumeration_matches_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(DictionaryViewEnumerationProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 4).Take(4).ToArray());
    }
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task Key_current_releases_values_while_value_current_retains_its_value(bool optimized, bool values)
    {
        var program = (await Programs.Value)[values ? 7 : 6]; var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(string name)
        {
            var method = flow.Methods.Single(m => m.Source.Name == "Cilica.Fixtures.Execution.DictionaryViewEnumerationProbe::" + name);
            return $"cilica_m_{method.Source.ContentHash}_{method.Source.Token:x8}";
        }
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0);
                Object* holder = nullptr; Frame frame; RootScope roots(c, frame, &holder, 1);
                holder = {{Symbol(values ? "CreateValues" : "CreateKeys")}}(c, nullptr);
                assert(c.pending == 0 && holder != nullptr);
                assert(collect(c).status == MarkStatus::complete && heap.count == 8);
                LocationArgument args[1]; args[0].reference = holder;
                {{Symbol("Clear")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == {{(values ? 7 : 6)}});
                {{Symbol(values ? "EndValues" : "EndKeys")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == 6);
                holder = nullptr; args[0].reference = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                assert(heap.emergency_exception != nullptr && c.used == 0 && c.borrows == nullptr);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("view-enumerator-gc"), cpp = Path.Combine(directory, "view.cpp"), executable = Path.Combine(directory, "view");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
