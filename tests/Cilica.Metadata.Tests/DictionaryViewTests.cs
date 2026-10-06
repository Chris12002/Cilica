using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class DictionaryViewTests
{
    private static readonly string[] Names = ["Identity", "Live", "ManualAndNull", "ReadOnlyFaults", "StringKeys", "ValuesAndReferences", "Tracked", "ValueSearch", "NullValues", "MutableValues"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(DictionaryViewProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Dictionary_views_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(DictionaryViewProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 4).Take(4).ToArray());
    }
    [Fact]
    public async Task Unknown_value_equality_remains_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(DictionaryViewProbe).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(DictionaryViewProbe).GetMethod("UnsupportedValuesContains")!.MetadataToken), implementation);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.ExecutionUnsupported && d.Message.Contains("reference fallbacks", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Retained_view_keeps_dictionary_contents_alive_and_cycle_is_collectible(bool optimized)
    {
        var program = (await Programs.Value)[6]; var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        var create = flow.Methods.Single(m => m.Source.Name == "Cilica.Fixtures.Execution.DictionaryViewProbe::CreateTracked");
        string symbol = $"cilica_m_{create.Source.ContentHash}_{create.Source.Token:x8}";
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0);
                Object* view = nullptr; Frame frame; RootScope roots(c, frame, &view, 1);
                view = {{symbol}}(c, nullptr);
                assert(c.pending == 0 && view != nullptr);
                assert(collect(c).status == MarkStatus::complete && heap.count == 7);
                // The view is the only explicit root: dictionary, entry, arrays
                // and value remain alive through it, including the cache cycle.
                view = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                assert(heap.emergency_exception != nullptr && c.used == 0 && c.borrows == nullptr);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("dictionary-view-gc"), cpp = Path.Combine(directory, "view.cpp"), executable = Path.Combine(directory, "view");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
    [Fact]
    public async Task Nested_view_interfaces_and_public_members_match_reference_metadata()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var contract = resolver.ResolveAssembly(plan.Bindings[0].Contract.Assembly).Value!;
        using var sourceStream = File.OpenRead(LibraryBuildProducerTests.Request(files, CoreLibraryBuildTests.CorePath("CoreTypes.cs")).References.Single());
        using var targetStream = File.OpenRead(built.AssemblyPath);
        using var sourceImage = new PEReader(sourceStream); using var targetImage = new PEReader(targetStream);
        var sourceReader = sourceImage.GetMetadataReader(); var targetReader = targetImage.GetMetadataReader();
        foreach (string name in new[] { "KeyCollection", "ValueCollection" })
        {
            var source = resolver.ResolveType(contract, "System.Collections.Generic", "Dictionary`2", name).Value!;
            var target = resolver.ResolveType(implementation, "System.Collections.Generic", "Dictionary`2", name).Value!;
            var expected = sourceReader.GetTypeDefinition(source.Handle).GetInterfaceImplementations().Select(h =>
                resolver.ResolveLibrarySignatureType(plan, contract, sourceReader.GetInterfaceImplementation(h).Interface).Value).ToHashSet();
            var actual = targetReader.GetTypeDefinition(target.Handle).GetInterfaceImplementations().Select(h =>
                resolver.ResolveSignatureType(implementation, targetReader.GetInterfaceImplementation(h).Interface).Value).ToHashSet();
            Assert.DoesNotContain(null, expected); Assert.True(expected.SetEquals(actual));
            foreach (string member in new[] { ".ctor", "get_Count", "CopyTo", "GetEnumerator" })
            {
                var method = Assert.Single(sourceReader.GetTypeDefinition(source.Handle).GetMethods(), h => sourceReader.GetString(sourceReader.GetMethodDefinition(h).Name) == member);
                var mapped = resolver.ResolveLibraryMethod(plan, contract, method);
                Assert.True(mapped.Success, string.Join("; ", mapped.Diagnostics));
            }
        }
    }
}
