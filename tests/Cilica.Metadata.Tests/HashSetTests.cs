using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class HashSetTests
{
    private static readonly string[] Names = ["Integers", "Clear", "Strings", "Collisions", "Faults", "Interfaces", "NullSet", "GuidKeys"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(HashSetProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)] [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Default_hash_set_operations_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(HashSetProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Reentrant_removal_preserves_untraced_value_slots(bool optimized)
    {
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(HashSetProbe), ["ReentrantRemoval"]);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("ReentrantRemoval", programs[0], HashSetProbe.ReentrantRemoval)]);
    }

    [Fact]
    public async Task Hash_set_requires_exact_key_grants_and_preserves_unsupported_boundaries()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var input = resolver.OpenInput(typeof(HashSetProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(HashSetProbe).GetMethod(name)!.MetadataToken);
        foreach (var capability in new[] { LibraryIntrinsicCapability.ClosedKeyEqual, LibraryIntrinsicCapability.ClosedKeyHash, LibraryIntrinsicCapability.ClosedKeyIsNull, LibraryIntrinsicCapability.ClosedTypeContainsReferences })
        {
            var grant = Assert.Single(artifact.Grants.Where(g => g.Capability == capability));
            var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(manifest with { Artifacts = [artifact with { Grants = artifact.Grants.Remove(grant) }] });
            using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
            var result = ValueTranslator.TranslateIntegerObjects(denied, input, Method("Strings"), implementation);
            Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        foreach (string name in new[] { "UnsupportedEnumeration", "UnsupportedComparer" })
            Assert.False(ValueTranslator.TranslateIntegerObjects(scope, input, Method(name), implementation).Success);
        var b = new ResolutionAssemblyBuilder(new("ExternalReferenceShape", new Version(1,0,0,0)));
        var owner = b.TypeReference("Cilica.Runtime", "KeyOperations`1", b.Reference(implementation.Identity));
        var spec = b.Specification(0x15, 0x12, checked((byte)((MetadataTokens.GetRowNumber(owner) << 2) | 1)), 1, 8);
        var call = b.Member(spec, "ContainsReferences", 0, 0, 2);
        b.Type("Example", "Caller"); var root = b.Method("Run", [0,0,8], emit: il => { il.Call(call); il.OpCode(ILOpCode.Ret); });
        var hostile = resolver.OpenInput(files.Write("external-shape.dll", b.Build())).Value!;
        var rejected = ValueTranslator.TranslateIntegerObjects(scope, hostile, root, implementation);
        Assert.False(rejected.Success); Assert.Contains(rejected.Diagnostics, d => d.Message.Contains("lexical declaring scope", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Removed_nested_key_references_are_collectible_while_the_set_is_rooted(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(HashSetProbe), ["Tracked"]))[0];
        var flow = program.Flow.Flow.Flow; var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(string name)
        {
            var method = flow.Methods.Single(m => m.Source.Name == "Cilica.Fixtures.Execution.HashSetProbe::" + name);
            return $"cilica_m_{method.Source.ContentHash}_{method.Source.Token:x8}";
        }
        string source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0);
                Object* set = nullptr; Frame frame; RootScope roots(c, frame, &set, 1);
                set = {{Symbol("CreateTracked")}}(c, nullptr);
                assert(c.pending == 0 && set != nullptr && collect(c).status == MarkStatus::complete);
                auto before = heap.count;
                LocationArgument args[1]; args[0].reference = set;
                {{Symbol("RemoveTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before - 1);
                {{Symbol("ClearTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before - 2);
                set = nullptr; args[0].reference = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1 && heap.emergency_exception != nullptr);
                assert(c.used == 0 && c.borrows == nullptr);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("hash-set-gc"), cpp = Path.Combine(directory, "set.cpp"), executable = Path.Combine(directory, "set");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
