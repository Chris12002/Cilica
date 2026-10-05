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
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class EmptyArrayLibraryTests
{
    private static readonly (string Name, Func<int, int, int> Oracle)[] Probes = [
        ("Identity", EmptyArrayProbe.Identity), ("Covariance", EmptyArrayProbe.Covariance), ("Faults", EmptyArrayProbe.Faults), ("ValueIdentity", EmptyArrayProbe.ValueIdentity)];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(EmptyArrayProbe), Probes.Select(p => p.Name).ToArray()));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Exact_empty_array_identity_covariance_and_faults_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            Probes.Select((p, i) => (p.Name, programs[i], p.Oracle)).ToArray());
    }
    [Fact]
    public async Task Exact_grant_is_required_and_unsupported_element_identity_is_not_collapsed()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions {
            Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != LibraryIntrinsicCapability.ArrayEmpty)] }] };
        var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
        var input = resolver.OpenInput(typeof(EmptyArrayProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(EmptyArrayProbe).GetMethod(name)!.MetadataToken);
        using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(denied, input, Method("Identity"), implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        foreach (var name in new[] { "UnsupportedUnsigned", "UnsupportedWide" })
        {
            var unsupported = ValueTranslator.TranslateIntegerObjects(scope, input, Method(name), implementation);
            Assert.False(unsupported.Success);
            Assert.Contains(unsupported.Diagnostics, d => d.Message.Contains("vector elements require Int32", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Cache_is_instance_owned_traced_and_bounded_under_corruption(bool optimized)
    {
        var program = (await Programs.Value)[0];
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        var array = program.Flow.Flow.Flow.Types.References.Arrays.Single(a => a.ArrayElement!.Kind == ObjectArrayElementKind.Int32);
        string descriptor = $"t_{array.ContentHash}_{array.Token:x8}_vector_i4_descriptor";
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                heap.limit = sizeof(ArrayObject) - 1;
                assert(empty_array(c, {{descriptor}}) == nullptr && heap.count == 0 && heap.empty_arrays == nullptr);
                heap.limit = sizeof(ArrayObject); c.pending = 0;
                auto* first = empty_array(c, {{descriptor}});
                assert(first != nullptr && heap.count == 1);
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                assert(empty_array(c, {{descriptor}}) == first && heap.count == 1);
                Instance other; Context d(other);
                auto* second = empty_array(d, {{descriptor}});
                assert(second != nullptr && second != first);
                other.empty_arrays = heap.empty_arrays;
                assert(empty_array(d, {{descriptor}}) == nullptr && d.pending == 6);
                assert(collect(d).status == MarkStatus::invalid);
                other.empty_arrays = reinterpret_cast<ArrayObject*>(second);
                auto* entry = heap.empty_arrays;
                entry->empty_next = entry;
                assert(collect(c).status == MarkStatus::invalid);
                entry->empty_next = nullptr;
                entry->length = 1;
                assert(empty_array(c, {{descriptor}}) == nullptr && c.pending == 6);
                assert(collect(c).status == MarkStatus::invalid);
                entry->length = 0; c.pending = 0;
                heap.empty_arrays = reinterpret_cast<ArrayObject*>(std::uintptr_t{1});
                assert(empty_array(c, {{descriptor}}) == nullptr && c.pending == 6);
                assert(collect(c).status == MarkStatus::invalid);
                heap.empty_arrays = entry; heap.marking = true; c.pending = 0;
                assert(empty_array(c, {{descriptor}}) == nullptr && c.pending == 6);
                heap.marking = false; c.pending = 0;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("empty-cache");
        string cpp = Path.Combine(directory, "empty.cpp"), exe = Path.Combine(directory, "empty");
        File.WriteAllText(cpp, source);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }

    [Theory]
    [InlineData("type")] [InlineData("array")] [InlineData("fault")]
    public async Task Detached_empty_arrays_require_exact_vector_results_and_fault_edges(string mutation)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        var method = flow.Methods.First(m => m.Blocks.Any(b => b.Statements.Any(s => s is IrStatement.Define { Value.Expression: IrExpression.EmptyArray })));
        var block = method.Blocks.First(b => b.Statements.Any(s => s is IrStatement.Define { Value.Expression: IrExpression.EmptyArray }));
        var statement = (IrStatement.Define)block.Statements.First(s => s is IrStatement.Define { Value.Expression: IrExpression.EmptyArray });
        var value = statement.Value;
        var changed = mutation switch {
            "type" => value with { Type = IrType.Int32 },
            "array" => value with { Expression = new IrExpression.EmptyArray(flow.Types.References.ObjectBase) },
            _ => value };
        var badBlock = block with { Statements = block.Statements.Replace(statement, statement with { Value = changed, Exceptional = mutation == "fault" ? null : statement.Exceptional }) };
        var badMethod = method with { Blocks = method.Blocks.Replace(block, badBlock) };
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Methods = flow.Methods.Replace(method, badMethod) } } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
    }
}
