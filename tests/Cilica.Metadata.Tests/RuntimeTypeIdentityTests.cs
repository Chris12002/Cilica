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

public sealed class RuntimeTypeIdentityTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(RuntimeTypeProbe), "Identity", "HandlesAndArrays", "Dictionary", "LiteralOnly", "ScalarTokens", "ObjectHash"));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Canonical_types_handles_and_dictionary_keys_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("Identity", programs[0], RuntimeTypeProbe.Identity),
             ("HandlesAndArrays", programs[1], RuntimeTypeProbe.HandlesAndArrays),
             ("Dictionary", programs[2], RuntimeTypeProbe.Dictionary),
             ("LiteralOnly", programs[3], RuntimeTypeProbe.LiteralOnly), ("ScalarTokens", programs[4], RuntimeTypeProbe.ScalarTokens),
             ("ObjectHash", programs[5], RuntimeTypeProbe.ObjectHash)]);
    }

    [Fact]
    public async Task Exact_grants_and_bounded_metadata_shapes_are_required()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions {
            Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var input = resolver.OpenInput(typeof(RuntimeTypeProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(RuntimeTypeProbe).GetMethod(name)!.MetadataToken);
        foreach (var capability in new[] { LibraryIntrinsicCapability.RuntimeTypeLayout, LibraryIntrinsicCapability.RuntimeTypeObjectLayout,
            LibraryIntrinsicCapability.RuntimeTypeHandleLayout, LibraryIntrinsicCapability.ObjectGetType,
            LibraryIntrinsicCapability.RuntimeTypeHash, LibraryIntrinsicCapability.ObjectIdentityHash })
        {
            var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != capability)] }] };
            var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
            using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
            var result = ValueTranslator.TranslateIntegerObjects(denied, input, Method(capability == LibraryIntrinsicCapability.ObjectIdentityHash ? "ObjectHash" : "Identity"), implementation);
            Assert.False(result.Success, capability.ToString());
            if (capability == LibraryIntrinsicCapability.ObjectIdentityHash)
                Assert.True(ValueTranslator.TranslateIntegerObjects(denied, input, Method("Identity"), implementation).Success);
            Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        foreach (var name in new[] { "UnsupportedReflection", "UnsupportedDefaultValueEquality", "UnsupportedStringHash", "UnsupportedArrayHash", "UnsupportedObjectAliasEquality" })
        {
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, Method(name), implementation);
            Assert.False(result.Success, name); Assert.Null(result.Program); Assert.NotEmpty(result.Diagnostics);
        }
        var hostile = new ResolutionAssemblyBuilder(new("IdentityIntrinsicCaller", new Version(1, 0, 0, 0)));
        var helper = hostile.TypeReference("Cilica.Runtime", "TypeIdentity", hostile.Reference(implementation.Identity));
        var call = hostile.Member(helper, "ObjectHash", 0, 1, 8, 0x1c);
        hostile.Type("Test", "Caller");
        var root = hostile.Method("Run", [0, 0, 8], emit: il => { il.OpCode(ILOpCode.Ldnull); il.Call(call); il.OpCode(ILOpCode.Ret); });
        var caller = scope.OpenInput(files.Write("identity-caller.dll", hostile.Build())).Value!;
        var deniedCaller = ValueTranslator.TranslateIntegerObjects(scope, caller, root, implementation);
        Assert.False(deniedCaller.Success);
        Assert.Contains(deniedCaller.Diagnostics, d => d.Message.Contains("Explicit type use requires lexical declaring scope", StringComparison.Ordinal));
        var abstractCaller = new ResolutionAssemblyBuilder(new("AbstractIdentityCaller", new Version(1, 0, 0, 0)));
        var abstractType = abstractCaller.TypeReference("Cilica.Fixtures.Execution", "TypeAbstractBase", abstractCaller.Reference(input.Identity));
        var constructor = abstractCaller.Member(abstractType, ".ctor", 0x20, 0, 1);
        abstractCaller.Type("Test", "Caller");
        var abstractRoot = abstractCaller.Method("Run", [0, 0, 8], emit: il => {
            il.OpCode(ILOpCode.Newobj); il.Token(constructor); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var abstractInput = scope.OpenInput(files.Write("abstract-identity-caller.dll", abstractCaller.Build())).Value!;
        var deniedAllocation = ValueTranslator.TranslateIntegerObjects(scope, abstractInput, abstractRoot, implementation);
        Assert.False(deniedAllocation.Success);
        Assert.Contains(deniedAllocation.Diagnostics, d => d.Message.Contains("Cannot allocate an abstract class", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Detached_identity_operations_reject_forged_targets_implementations_and_missing_fault_edges()
    {
        var program = (await Programs.Value)[0];
        var flow = program.Flow.Flow.Flow;
        var method = flow.Methods.First(m => m.Blocks.Any(b => b.Values.Any(v => v.Expression is IrExpression.RuntimeTypeLiteral)));
        var block = method.Blocks.First(b => b.Values.Any(v => v.Expression is IrExpression.RuntimeTypeLiteral));
        var statement = block.Statements.OfType<IrStatement.Define>().First(s => s.Value.Expression is IrExpression.RuntimeTypeLiteral);
        var literal = (IrExpression.RuntimeTypeLiteral)statement.Value.Expression;
        foreach (var replacement in new[] {
            statement with { Value = statement.Value with { Expression = literal with { Implementation = flow.Types.References.ObjectBase } } },
            statement with { Value = statement.Value with { Expression = literal with { Target = literal.Target with { Token = 0x0200ffff } } } },
            statement with { Exceptional = null } })
        {
            var changedBlock = block with { Statements = block.Statements.Replace(statement, replacement) };
            var changedMethod = method with { Blocks = method.Blocks.Replace(block, changedBlock) };
            var changed = program with { Flow = program.Flow with { Flow = program.Flow.Flow with {
                Flow = flow with { Methods = flow.Methods.Replace(method, changedMethod) } } } };
            Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(changed));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Type_cache_is_instance_owned_budgeted_and_traced(bool optimized)
    {
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions((await Programs.Value)[0]));
        var implementation = (await Programs.Value)[0].Flow.Flow.Flow.Types.References.Types.Single(t => t.Source.Name == "Cilica.Runtime.RuntimeTypeObject").Source;
        string type = $"t_{implementation.ContentHash}_{implementation.Token:x8}";
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                const auto cache_bytes = runtime_type_count * sizeof(Object*);
                heap.limit = cache_bytes - 1;
                assert(runtime_type<{{type}}>(c, {{type}}_descriptor, 0) == nullptr);
                assert(heap.runtime_types == nullptr && heap.bytes == 0 && heap.count == 0);
                heap.limit = cache_bytes;
                assert(runtime_type<{{type}}>(c, {{type}}_descriptor, 0) == nullptr);
                assert(heap.runtime_types != nullptr && heap.bytes == cache_bytes && heap.count == 0);
                heap.limit = cache_bytes + sizeof({{type}});
                auto* first = runtime_type<{{type}}>(c, {{type}}_descriptor, 0);
                assert(first != nullptr && heap.count == 1);
                assert(runtime_type<{{type}}>(c, {{type}}_descriptor, 0) == first);
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                assert(runtime_type<{{type}}>(c, {{type}}_descriptor, 1) == nullptr && heap.count == 1);
                Instance other; Context d(other);
                auto* second = runtime_type<{{type}}>(d, {{type}}_descriptor, 0);
                assert(second != nullptr && first != second);
                other.runtime_types[0] = first;
                assert(runtime_type<{{type}}>(d, {{type}}_descriptor, 0) == nullptr && d.pending == 6);
                assert(collect(d).status == MarkStatus::invalid);
                other.runtime_types[0] = second; d.pending = 0;
                assert(collect(d).status == MarkStatus::complete);
                assert(runtime_type<{{type}}>(d, {{type}}_descriptor, runtime_type_count) == nullptr && d.pending == 6);
            }
            """;
        using var files = new ResolverFiles();
        string directory = files.DirectoryPath("runtime-types"), cpp = Path.Combine(directory, "cache.cpp"), exe = Path.Combine(directory, "cache");
        File.WriteAllText(cpp, source);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
             "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
