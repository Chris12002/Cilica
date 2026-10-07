using System;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class DictionaryLibraryTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Fixture = new(BuildPrograms);

    private static async Task<ManagedExceptionIrProgram[]> BuildPrograms()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(DictionaryProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(DictionaryProbe).GetMethod(name)!.MetadataToken);
        var programs = new System.Collections.Generic.List<ManagedExceptionIrProgram>();
        foreach (string name in new[] { "Integers", "WideKeys", "Guids", "Collisions", "CallbackFaults", "Interfaces" })
        {
            var method = Method(name);
            var translated = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
            Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
            programs.Add(translated.Program!);
        }
        var unsupported = Method("UnsupportedReferenceKey");
        var rejected = ValueTranslator.TranslateIntegerObjects(scope, input, unsupported, implementation);
        Assert.False(rejected.Success); Assert.Null(rejected.Program);
        Assert.Contains(rejected.Diagnostics, d => d.Message.Contains("reference fallbacks", StringComparison.Ordinal));
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions {
            Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() }
        })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var integers = Method("Integers");
        foreach (var capability in new[] { LibraryIntrinsicCapability.ClosedKeyEqual, LibraryIntrinsicCapability.ClosedKeyHash, LibraryIntrinsicCapability.ClosedKeyIsNull })
        {
            var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != capability)] }] };
            var narrowedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
            using var deniedScope = resolver.CreateTrustedLibraryCompilationScope(plan, narrowedBytes, Convert.ToHexStringLower(SHA256.HashData(narrowedBytes))).Value!;
            var denied = ValueTranslator.TranslateIntegerObjects(deniedScope, input, integers, implementation);
            Assert.False(denied.Success);
            Assert.Contains(denied.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
        var hostile = new ResolutionAssemblyBuilder(new("KeyIntrinsicCaller", new Version(1, 0, 0, 0)));
        var helper = hostile.TypeReference("Cilica.Runtime", "KeyOperations`1", hostile.Reference(implementation.Identity));
        var specialization = hostile.Specification(0x15, 0x12, checked((byte)((MetadataTokens.GetRowNumber(helper) << 2) | 1)), 1, 8);
        var call = hostile.Member(specialization, "IsNull", 0, 1, 2, 0x13, 0);
        hostile.Type("Test", "Caller");
        var root = hostile.Method("Run", [0, 0, 8], emit: il => {
            il.LoadConstantI4(0); il.Call(call); il.OpCode(ILOpCode.Ret);
        });
        var caller = scope.OpenInput(files.Write("key-caller.dll", hostile.Build())).Value!;
        var deniedCaller = ValueTranslator.TranslateIntegerObjects(scope, caller, root, implementation);
        Assert.False(deniedCaller.Success);
        Assert.Contains(deniedCaller.Diagnostics, d => d.Message.Contains("lexical declaring scope", StringComparison.Ordinal));
        return programs.ToArray();
    }

    [Fact]
    public async Task Dictionary_nominal_interfaces_and_admitted_members_match_reference_metadata()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var contract = resolver.ResolveAssembly(plan.Bindings[0].Contract.Assembly).Value!;
        using var sourceStream = File.OpenRead(LibraryBuildProducerTests.Request(files, CoreLibraryBuildTests.CorePath("CoreTypes.cs")).References.Single());
        using var targetStream = File.OpenRead(built.AssemblyPath);
        using var sourceImage = new PEReader(sourceStream); using var targetImage = new PEReader(targetStream);
        var sourceReader = sourceImage.GetMetadataReader(); var targetReader = targetImage.GetMetadataReader();
        var source = resolver.ResolveType(contract, "System.Collections.Generic", "Dictionary`2").Value!;
        var target = resolver.ResolveType(implementation, "System.Collections.Generic", "Dictionary`2").Value!;
        var expected = sourceReader.GetTypeDefinition(source.Handle).GetInterfaceImplementations().Select(h =>
            resolver.ResolveLibrarySignatureType(plan, contract, sourceReader.GetInterfaceImplementation(h).Interface).Value).ToHashSet();
        var actual = targetReader.GetTypeDefinition(target.Handle).GetInterfaceImplementations().Select(h =>
            resolver.ResolveSignatureType(implementation, targetReader.GetInterfaceImplementation(h).Interface).Value).ToHashSet();
        Assert.DoesNotContain(null, expected); Assert.True(expected.SetEquals(actual));
        foreach (var (name, count) in new[] { (".ctor", 0), ("get_Count", 0), ("TryAdd", 2), ("Add", 2), ("get_Item", 1), ("TryGetValue", 2), ("ContainsKey", 1), ("ContainsValue", 1), ("get_Keys", 0), ("get_Values", 0), ("GetEnumerator", 0), ("set_Item", 2), ("Remove", 1), ("Remove", 2), ("Clear", 0) })
        {
            var method = Assert.Single(sourceReader.GetTypeDefinition(source.Handle).GetMethods(), h => {
                var definition = sourceReader.GetMethodDefinition(h);
                var signature = resolver.ResolveMethod(contract, h).Value;
                return sourceReader.GetString(definition.Name) == name && signature?.Signature.Parameters.Count == count;
            });
            var mapped = resolver.ResolveLibraryMethod(plan, contract, method);
            Assert.True(mapped.Success, string.Join("; ", mapped.Diagnostics));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Dictionary_growth_collisions_copies_and_mutating_key_callbacks_match_dotnet(bool optimized)
    {
        var programs = await Fixture.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [
            ("Integers", programs[0], DictionaryProbe.Integers), ("WideKeys", programs[1], DictionaryProbe.WideKeys),
            ("Guids", programs[2], DictionaryProbe.Guids), ("Collisions", programs[3], DictionaryProbe.Collisions),
            ("CallbackFaults", programs[4], DictionaryProbe.CallbackFaults), ("Interfaces", programs[5], DictionaryProbe.Interfaces)]);
    }
}
