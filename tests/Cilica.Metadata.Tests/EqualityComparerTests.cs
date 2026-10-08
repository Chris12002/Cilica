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
using Cilica.Metadata.Resolution;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class EqualityComparerTests
{
    private static readonly string[] Names = ["Signed", "Unsigned", "Wide", "UnsignedWide", "Values", "References", "Typed", "Custom", "Null", "Identity", "PrimitiveEquals"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(EqualityComparerProbe), Names));
    [Fact]
    public async Task Comparer_selection_uses_existing_key_bodies()
    {
        foreach (var program in await Programs.Value)
        {
            ManagedExceptionIrValidator.Validate(program);
            ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(program));
            Assert.DoesNotContain(program.Flow.Flow.Flow.Methods, m => m.Source.Name.Contains("ComparerOperations", StringComparison.Ordinal));
        }
    }
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)] [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    public async Task Typed_equality_hashing_and_identity_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        foreach (var index in Enumerable.Range(group * 3, Math.Min(3, Names.Length - group * 3)))
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,
                [(Names[index], programs[index], typeof(EqualityComparerProbe).GetMethod(Names[index])!.CreateDelegate<Func<int,int,int>>())],
                expectedStaticRoots: Names[index] == "Identity" ? 4 : Names[index] == "PrimitiveEquals" ? 0 : 1);
    }
    [Theory]
    [InlineData("UnsupportedString")] [InlineData("UnsupportedNullable")] [InlineData("UnsupportedEnum")]
    [InlineData("UnsupportedByte")] [InlineData("UnsupportedFallback")]
    [InlineData("UnsupportedObject")] [InlineData("UnsupportedHash")]
    public async Task Unimplemented_default_and_object_operations_remain_diagnostic(string name)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(EqualityComparerProbe).Assembly.Location).Value!;
        var handle = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(EqualityComparerProbe).GetMethod(name)!.MetadataToken);
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, handle, implementation);
        Assert.False(result.Success); Assert.NotEmpty(result.Diagnostics);
        if (name is not ("UnsupportedObject" or "UnsupportedHash"))
            Assert.Contains(result.Diagnostics, d => d.Message.Contains("Default equality comparer requires", StringComparison.Ordinal));
    }
    [Fact]
    public async Task Private_helpers_need_exact_grants_and_reject_application_calls()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var input = resolver.OpenInput(typeof(EqualityComparerProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(EqualityComparerProbe).GetMethod("Signed")!.MetadataToken);
        foreach (var capability in new[] { LibraryIntrinsicCapability.EquatableComparerCreate, LibraryIntrinsicCapability.ClosedKeyEqual, LibraryIntrinsicCapability.ClosedKeyHash })
        {
            var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != capability)] }] };
            var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
            using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
            var result = ValueTranslator.TranslateIntegerObjects(denied, input, method, implementation);
            Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        var hostile = new ResolutionAssemblyBuilder(new("ComparerIntrinsicCaller", new Version(1, 0, 0, 0)));
        var helper = hostile.TypeReference("Cilica.Runtime", "EqualityComparerOperations`1", hostile.Reference(implementation.Identity));
        var specialization = hostile.Specification(0x15, 0x12, checked((byte)((MetadataTokens.GetRowNumber(helper) << 2) | 1)), 1, 8);
        var comparer = hostile.TypeReference("System.Collections.Generic", "EqualityComparer`1", hostile.Reference(implementation.Identity));
        var call = hostile.Member(specialization, "Create", 0, 0, 0x15, 0x12, checked((byte)((MetadataTokens.GetRowNumber(comparer) << 2) | 1)), 1, 0x13, 0);
        hostile.Type("Test", "Caller");
        var root = hostile.Method("Run", [0, 2, 8, 8, 8], emit: il => {
            il.Call(call); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var caller = scope.OpenInput(files.Write("caller.dll", hostile.Build())).Value!;
        var deniedCaller = ValueTranslator.TranslateIntegerObjects(scope, caller, root, implementation);
        Assert.False(deniedCaller.Success);
        Assert.Contains(deniedCaller.Diagnostics, d => d.Message.Contains("lexical declaring scope", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Each_closed_default_has_one_root_owned_by_its_runtime_instance(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(EqualityComparerProbe), ["Identity"]))[0]; var flow = program.Flow.Flow.Flow;
        Assert.Equal(4, flow.Types.References.Statics.Length);
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string symbol = $"cilica_m_{flow.Entry.ContentHash}_{flow.Entry.Token:x8}";
        var source = artifact.Text + $$"""
            #include <cassert>
            #include <memory>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            static void collected(Context& c, std::size_t expected) {
                assert(collect(c).status == MarkStatus::complete && c.instance.count == expected);
                assert(c.used == 0 && c.borrows == nullptr && c.pending == 0);
                if (expected == 1) assert(c.instance.head == c.instance.emergency_exception);
            }
            static void owned_roots(Instance& heap, Instance& other) {
                for (int j = 0; j < 4; ++j) {
                    Object* first = heap.statics[j].reference; Object* second = other.statics[j].reference;
                    assert(first != nullptr && second != nullptr && first != second);
                    assert(heap.owns(first) && !other.owns(first));
                    assert(other.owns(second) && !heap.owns(second));
                    for (int k = 0; k < j; ++k) assert(first != heap.statics[k].reference);
                }
            }
            int main() {
                auto heapOwner = std::make_unique<Instance>(); Instance& heap = *heapOwner;
                heap.collect_before_allocation = true; Context c(heap);
                auto otherOwner = std::make_unique<Instance>(); Instance& other = *otherOwner;
                other.collect_before_allocation = true; Context d(other);
                assert(prepare_exceptions(c) == 0 && prepare_exceptions(d) == 0);
                collected(c, 1);
                collected(d, 1);
                LocationArgument args[2]; args[0].bits = 65; args[1].bits = 66;
                for (int i = 0; i < 3; i++) {
                    assert({{symbol}}(c, args) == 17 && c.pending == 0);
                    assert({{symbol}}(d, args) == 17 && d.pending == 0);
                    collected(c, 5); collected(d, 5); owned_roots(heap, other);
                }
                for (int j = 0; j < 4; ++j) { heap.statics[j].reference = nullptr; other.statics[j].reference = nullptr; }
                collected(c, 1);
                collected(d, 1);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("integer-comparer"), cpp = Path.Combine(directory, "comparer.cpp"), executable = Path.Combine(directory, "comparer");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
