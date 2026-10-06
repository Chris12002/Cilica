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
public sealed class ListSearchTests
{
    private static readonly string[] Names = ["Integers", "Strings", "Values", "ThrowingEquality", "Arrays", "ArrayValues", "EmptyRemove", "ReadOnly", "Mixed", "Nulls", "Tracked", "AllocatingEquality"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ListSearchProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Search_and_removal_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ListSearchProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Take(11).Skip(group * 4).Take(4).ToArray());
    }
    [Fact]
    public async Task Helpers_require_exact_grants_and_unknown_reference_equality_stays_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var input = resolver.OpenInput(typeof(ListSearchProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ListSearchProbe).GetMethod(name)!.MetadataToken);
        foreach (var capability in new[] { LibraryIntrinsicCapability.ArrayInterfaceIndexOf, LibraryIntrinsicCapability.ArrayInterfaceContains, LibraryIntrinsicCapability.ArrayInterfaceRemove, LibraryIntrinsicCapability.ClosedKeyEqual, LibraryIntrinsicCapability.ClosedArraySearch })
        {
            var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != capability)] }] };
            var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
            using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
            var result = ValueTranslator.TranslateIntegerObjects(denied, input, Method("Arrays"), implementation);
            Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        var unsupported = ValueTranslator.TranslateIntegerObjects(scope, input, Method("UnsupportedReference"), implementation);
        Assert.False(unsupported.Success); Assert.Contains(unsupported.Diagnostics, d => d.Message.Contains("reference fallbacks", StringComparison.Ordinal));
        var nullable = ValueTranslator.TranslateIntegerObjects(scope, input, Method("UnsupportedNullable"), implementation);
        Assert.False(nullable.Success); Assert.Contains(nullable.Diagnostics, d => d.Message.Contains("Nullable array search", StringComparison.Ordinal));
        var hostile = new ResolutionAssemblyBuilder(new("SearchIntrinsicCaller", new Version(1, 0, 0, 0)));
        var helper = hostile.TypeReference("Cilica.Runtime", "KeyOperations`1", hostile.Reference(implementation.Identity));
        var specialization = hostile.Specification(0x15, 0x12, checked((byte)((MetadataTokens.GetRowNumber(helper) << 2) | 1)), 1, 8);
        var call = hostile.Member(specialization, "IndexOf", 0, 3, 8, 0x1d, 0x13, 0, 0x13, 0, 8);
        hostile.Type("Test", "Caller");
        var root = hostile.Method("Run", [0, 0, 8], emit: il => {
            il.OpCode(ILOpCode.Ldnull); il.LoadConstantI4(0); il.LoadConstantI4(0); il.Call(call); il.OpCode(ILOpCode.Ret);
        });
        var caller = scope.OpenInput(files.Write("search-caller.dll", hostile.Build())).Value!;
        var deniedCaller = ValueTranslator.TranslateIntegerObjects(scope, caller, root, implementation);
        Assert.False(deniedCaller.Success);
        Assert.Contains(deniedCaller.Diagnostics, d => d.Message.Contains("private to the selected core", StringComparison.Ordinal));
    }
    [Fact]
    public async Task Detached_search_packets_preserve_scalar_results_and_value_leaves()
    {
        var programs = await Programs.Value;
        foreach (int index in new[] { 4, 5, 6 })
        {
            foreach (var plan in new[] { programs[index].Flow.InterfaceDispatch!, ValueScalarLowering.LowerExceptions(programs[index]).Flow.Flow.Flow.Flow.InterfaceDispatch! })
            {
                ObjectInterfacePlanValidator.Validate(plan);
                foreach (var fact in plan.Methods.Where(m => m.ArrayHelper && m.Name is "IndexOf" or "Contains" or "Remove"))
                    foreach (var changed in new[] { fact with { ReturnType = IrType.Void }, fact with { Parameters = fact.Parameters.Add(IrType.Int32) }, fact with { Name = "ClearReadOnly" } })
                        Assert.Throws<ArgumentException>(() => ObjectInterfacePlanValidator.Validate(plan with { Methods = plan.Methods.Replace(fact, changed) }));
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Removed_references_are_collectible_while_the_list_stays_rooted(bool optimized)
    {
        var program = (await Programs.Value)[10];
        var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(string name)
        {
            var method = flow.Methods.Single(m => m.Source.Name == "Cilica.Fixtures.Execution.ListSearchProbe::" + name);
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
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before - 2);
                {{Symbol("ClearTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before - 2);
                list = nullptr; args[0].reference = nullptr;
                // Default List construction leaves one permanent Array.Empty cache root.
                assert(collect(c).status == MarkStatus::complete && heap.count == 2 && heap.emergency_exception != nullptr);
                assert(heap.empty_arrays != nullptr && heap.empty_arrays->empty_next == nullptr);
                assert(c.used == 0 && c.borrows == nullptr);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("list-search-gc"), cpp = Path.Combine(directory, "list.cpp"), executable = Path.Combine(directory, "list");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Allocating_equality_keeps_the_borrowed_array_rooted(bool optimized)
    {
        var program = (await Programs.Value)[11];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("AllocatingEquality", program, ListSearchProbe.AllocatingEquality)]);
    }

}
