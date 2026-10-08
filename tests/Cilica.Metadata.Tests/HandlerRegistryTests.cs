using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class HandlerRegistryTests
{
    private static readonly string[] Names = ["Transcript", "Mutation", "Faults", "Returned", "Tracked"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(HandlerRegistryProbe), Names));

    [Fact]
    public async Task Closed_registry_dispatch_validates_at_both_ir_stages()
    {
        foreach (var program in await Programs.Value)
        {
            ManagedExceptionIrValidator.Validate(program);
            ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(program));
            Assert.Contains(program.Flow.InterfaceDispatch!.Declarations, d => d.Name == "Execute");
        }
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Ordered_events_state_identity_and_registry_faults_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        var pairs = (from a in new[] { int.MinValue, -31, -7, -1, 0, 1, 2, 7, 31, 65535, int.MaxValue }
                     from b in new[] { int.MinValue, -7, -1, 0, 1, 7, int.MaxValue } select (a, b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(HandlerRegistryProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).ToArray(), pairs);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Registry_releases_handlers_while_returned_event_stays_rooted(bool optimized)
    {
        var program = (await Programs.Value)[4]; var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(string name)
        {
            var method = flow.Methods.Single(m => m.Source.Name == "Cilica.Fixtures.Execution.HandlerRegistryProbe::" + name);
            return $"cilica_m_{method.Source.ContentHash}_{method.Source.Token:x8}";
        }
        string source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0);
                Object* live[2] = {nullptr, nullptr}; Frame frame; RootScope roots(c, frame, live, 2);
                live[0] = {{Symbol("CreateTracked")}}(c, nullptr);
                assert(c.pending == 0 && live[0] != nullptr && collect(c).status == MarkStatus::complete);
                auto before = heap.count;
                LocationArgument args[1]; args[0].reference = live[0];
                live[1] = {{Symbol("EmitTracked")}}(c, args);
                assert(c.pending == 0 && live[1] != nullptr && collect(c).status == MarkStatus::complete && heap.count == before + 3);
                {{Symbol("RemoveTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before + 1);
                {{Symbol("ReplaceTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before + 1);
                {{Symbol("ClearTracked")}}(c, args);
                // Clear releases the remaining handler/bias and both private entry objects.
                // The returned event still retains its payload and state.
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before - 3);
                {{Symbol("ClearTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before - 3);
                live[1] = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == before - 6);
                live[0] = nullptr; args[0].reference = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1 && heap.emergency_exception != nullptr);
                assert(c.used == 0 && c.borrows == nullptr);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("handler-registry-gc");
        string cpp = Path.Combine(directory, "registry.cpp"), executable = Path.Combine(directory, "registry");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        NativeEvidence.Record(cpp, optimized);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
             "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        NativeEvidence.Record(cpp, optimized, executable);
        var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
