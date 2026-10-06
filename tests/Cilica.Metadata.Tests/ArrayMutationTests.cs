using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ArrayMutationTests
{
    private static readonly string[] Names = ["Integers", "References", "Values", "EmptyValues", "LegacyInsert", "ClearIntegers", "ClearReferences", "ClearValues", "ClearEmpty", "Nulls", "MixedValues", "Tracked", "TrackedValues"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ArrayMutationProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    [InlineData(false, 3)] [InlineData(true, 3)]
    public async Task Array_mutations_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ArrayMutationProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 4).Take(4).ToArray());
    }
    [Fact]
    public async Task New_helpers_require_individual_exact_grants()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions {
            Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var input = resolver.OpenInput(typeof(ArrayMutationProbe).Assembly.Location).Value!;
        foreach (var (capability, name) in new[] {
            (LibraryIntrinsicCapability.ArrayInterfaceInsert, "Integers"),
            (LibraryIntrinsicCapability.ArrayInterfaceInsertObject, "LegacyInsert"),
            (LibraryIntrinsicCapability.ArrayInterfaceRemoveAt, "Integers"),
            (LibraryIntrinsicCapability.ArrayInterfaceClearReadOnly, "Integers"),
            (LibraryIntrinsicCapability.ArrayInterfaceClear, "ClearValues") })
        {
            Assert.Single(artifact.Grants.Where(g => g.Capability == capability));
            var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != capability)] }] };
            var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
            using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
            var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ArrayMutationProbe).GetMethod(name)!.MetadataToken);
            var result = ValueTranslator.TranslateIntegerObjects(denied, input, method, implementation);
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
    }
    [Fact]
    public async Task Detached_recipes_check_operation_index_and_value_packets()
    {
        var programs = await Programs.Value;
        foreach (int index in new[] { 0, 2, 3, 4, 5, 7 })
        {
            var logical = programs[index].Flow.InterfaceDispatch!;
            var scalar = ValueScalarLowering.LowerExceptions(programs[index]).Flow.Flow.Flow.Flow.InterfaceDispatch!;
            foreach (var plan in new[] { logical, scalar })
            {
                ObjectInterfacePlanValidator.Validate(plan);
                foreach (var fact in plan.Methods.Where(m => m.ArrayHelper && m.Name is "Insert" or "InsertObject" or "RemoveAt" or "ClearReadOnly" or "Clear"))
                {
                    // Extra/missing fields, wrong index/result types and swapped Clear semantics are rejected.
                    foreach (var changed in new[] {
                        fact with { Parameters = fact.Parameters.Add(IrType.Int32) },
                        fact with { Parameters = fact.Parameters.RemoveAt(fact.Parameters.Length - 1) },
                        fact with { ReturnType = IrType.Int32 },
                        fact with { Name = fact.Name == "Clear" ? "ClearReadOnly" : "Clear" } })
                        Assert.Throws<ArgumentException>(() => ObjectInterfacePlanValidator.Validate(plan with { Methods = plan.Methods.Replace(fact, changed) }));
                    if (fact.Parameters.Length > 2)
                    {
                        int last = fact.Parameters.Length - 1;
                        var wrongType = fact.Parameters[last] == IrType.Int64 ? IrType.Int32 : IrType.Int64;
                        var changed = fact with { Parameters = fact.Parameters.SetItem(last, wrongType) };
                        Assert.Throws<ArgumentException>(() => ObjectInterfacePlanValidator.Validate(plan with { Methods = plan.Methods.Replace(fact, changed) }));
                    }
                    if (fact.Parameters.Length > 1)
                    {
                        var changed = fact with { Parameters = fact.Parameters.SetItem(1, IrType.Int64) };
                        Assert.Throws<ArgumentException>(() => ObjectInterfacePlanValidator.Validate(plan with { Methods = plan.Methods.Replace(fact, changed) }));
                    }
                }
            }
        }
    }
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task Clear_releases_references_while_the_array_stays_rooted(bool optimized, bool valueElements)
    {
        var program = (await Programs.Value)[valueElements ? 12 : 11];
        var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(string name)
        {
            var method = flow.Methods.Single(m => m.Source.Name == "Cilica.Fixtures.Execution.ArrayMutationProbe::" + name + (valueElements ? "Values" : ""));
            return $"cilica_m_{method.Source.ContentHash}_{method.Source.Token:x8}";
        }
        string source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0);
                Object* array = nullptr; Frame frame; RootScope roots(c, frame, &array, 1);
                array = {{Symbol("CreateTracked")}}(c, nullptr);
                assert(c.pending == 0 && array != nullptr && collect(c).status == MarkStatus::complete);
                auto before = heap.count;
                LocationArgument args[1]; args[0].reference = array;
                {{Symbol("ClearTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before - 2);
                {{Symbol("ClearTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before - 2);
                array = nullptr; args[0].reference = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1 && heap.emergency_exception != nullptr);
                assert(c.used == 0 && c.borrows == nullptr);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("array-gc"), cpp = Path.Combine(directory, "array.cpp"), executable = Path.Combine(directory, "array");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
