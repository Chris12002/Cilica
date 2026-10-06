using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class YieldIteratorTests
{
    private static readonly string[] Names = ["ThreadIdentity", "Integers", "Reuse", "CurrentAndReset", "Disposal", "Finally", "References", "Values"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(YieldIteratorProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Single_thread_identity_and_yield_state_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(YieldIteratorProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }

    [Fact]
    public async Task Other_environment_and_thread_contracts_remain_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(YieldIteratorProbe).Assembly.Location).Value!;
        foreach (var (name, message) in new[] { ("UnsupportedEnvironment", "No implementation method matches"), ("UnsupportedThread", "has no library mapping") })
        {
            var handle = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(YieldIteratorProbe).GetMethod(name)!.MetadataToken);
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, handle, implementation);
            Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains(message, StringComparison.Ordinal));
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Suspended_iterator_roots_and_managed_identity_survive_serial_host_thread_changes(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(YieldIteratorProbe), ["Tracked"]))[0];
        var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(string name) { var method = flow.Methods.Single(m => m.Source.Name == "Cilica.Fixtures.Execution.YieldIteratorProbe::" + name); return $"cilica_m_{method.Source.ContentHash}_{method.Source.Token:x8}"; }
        var source = artifact.Text + $$"""
            #include <cassert>
            #include <thread>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0);
                Object* iterator = nullptr; Frame frame; RootScope roots(c, frame, &iterator, 1);
                std::thread create([&] { iterator = {{Symbol("MakeTracked")}}(c, nullptr); }); create.join();
                assert(c.pending == 0 && iterator != nullptr);
                assert(collect(c).status == MarkStatus::complete && heap.count == 2);
                LocationArgument args[1]; args[0].reference = iterator;
                std::thread acquire([&] { assert({{Symbol("BindTracked")}}(c, args) == iterator && c.pending == 0); }); acquire.join();
                {{Symbol("MoveTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == 3);
                {{Symbol("MoveTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == 2);
                {{Symbol("MoveTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == 3);
                {{Symbol("MoveTracked")}}(c, args); {{Symbol("StopTracked")}}(c, args);
                // Roslyn's current field remains populated after end/disposal.
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == 3);
                iterator = nullptr; args[0].reference = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                assert(c.used == 0 && c.borrows == nullptr);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("yield-gc"), cpp = Path.Combine(directory, "iterator.cpp"), executable = Path.Combine(directory, "iterator");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
