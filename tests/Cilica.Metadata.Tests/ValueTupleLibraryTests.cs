using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
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
public sealed class ValueTupleLibraryTests
{
    private static readonly (string Name, Func<int, int, int> Oracle)[] Probes = [
        ("Values", ValueTupleProbe.Values), ("Dictionary", ValueTupleProbe.Dictionary), ("CopyAndHash", ValueTupleProbe.CopyAndHash), ("Strings", ValueTupleProbe.Strings)];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(ValueTupleProbe), Probes.Select(p => p.Name).ToArray()));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Tuple_copy_equality_dictionary_and_string_keys_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Probes.Select((p, i) => (p.Name, programs[i], p.Oracle)).ToArray());
    }
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(5)] [InlineData(8)]
    public async Task Tuple_nominal_contract_and_fields_match_reference_metadata(int arity)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var contract = resolver.ResolveAssembly(plan.Bindings[0].Contract.Assembly).Value!;
        using var sourceStream = File.OpenRead(LibraryBuildProducerTests.Request(files, CoreLibraryBuildTests.CorePath("CoreTypes.cs")).References.Single());
        using var targetStream = File.OpenRead(built.AssemblyPath);
        using var sourceImage = new PEReader(sourceStream); using var targetImage = new PEReader(targetStream);
        var sourceReader = sourceImage.GetMetadataReader(); var targetReader = targetImage.GetMetadataReader();
        var source = resolver.ResolveType(contract, "System", "ValueTuple`" + arity).Value!;
        var target = resolver.ResolveType(implementation, "System", "ValueTuple`" + arity).Value!;
        var expected = sourceReader.GetTypeDefinition(source.Handle).GetInterfaceImplementations().Select(h =>
            resolver.ResolveLibrarySignatureType(plan, contract, sourceReader.GetInterfaceImplementation(h).Interface).Value).ToHashSet();
        var actual = targetReader.GetTypeDefinition(target.Handle).GetInterfaceImplementations().Select(h =>
            resolver.ResolveSignatureType(implementation, targetReader.GetInterfaceImplementation(h).Interface).Value).ToHashSet();
        Assert.DoesNotContain(null, expected); Assert.True(expected.SetEquals(actual)); Assert.Equal(6, actual.Count);
        foreach (var handle in sourceReader.GetTypeDefinition(source.Handle).GetFields())
        {
            var result = resolver.ResolveLibraryField(plan, contract, handle);
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        }
        foreach (var handle in sourceReader.GetTypeDefinition(source.Handle).GetMethods())
        {
            string name = sourceReader.GetString(sourceReader.GetMethodDefinition(handle).Name);
            if (name != ".ctor" && name != "Equals" && name != "GetHashCode") continue;
            var result = resolver.ResolveLibraryMethod(plan, contract, handle);
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        }
    }
    [Fact]
    public async Task String_key_selection_requires_exact_grants_and_unimplemented_tuple_members_fail()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var input = resolver.OpenInput(typeof(ValueTupleProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ValueTupleProbe).GetMethod(name)!.MetadataToken);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        foreach (string name in new[] { "UnsupportedCompare", "UnsupportedObjectEquals", "UnsupportedText" })
        {
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, Method(name), implementation);
            Assert.False(result.Success); Assert.Null(result.Program);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains("no implemented managed body", StringComparison.Ordinal));
        }
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions {
            Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        foreach (var capability in new[] { LibraryIntrinsicCapability.ClosedKeyEqual, LibraryIntrinsicCapability.ClosedKeyHash, LibraryIntrinsicCapability.ClosedKeyIsNull })
        {
            var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != capability)] }] };
            var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
            using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
            var result = ValueTranslator.TranslateIntegerObjects(denied, input, Method("Strings"), implementation);
            Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
    }
}
