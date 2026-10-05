using System;
using System.Linq;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ListLibraryTests
{
    private static readonly (string Name, Func<int, int, int> Oracle)[] Probes = [
        ("ArrayMutations", ListLibraryProbe.ArrayMutations), ("Growth", ListLibraryProbe.Growth), ("References", ListLibraryProbe.References),
        ("LiveView", ListLibraryProbe.LiveView), ("Capacity", ListLibraryProbe.Capacity), ("Faults", ListLibraryProbe.Faults)];
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Managed_list_growth_and_live_readonly_views_match_dotnet(bool optimized)
    {
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(ListLibraryProbe), Probes.Select(p => p.Name).ToArray());
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            Probes.Select((p, i) => (p.Name, programs[i], p.Oracle)).ToArray());
    }
    [Fact]
    public async Task Unimplemented_list_members_are_build_diagnostics()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await System.IO.File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(ListLibraryProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ListLibraryProbe).GetMethod("UnsupportedContains")!.MetadataToken);
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("no implemented managed body", StringComparison.Ordinal));
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var mutation = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ListLibraryProbe).GetMethod("ArrayMutations")!.MetadataToken);
        foreach (var capability in new[] { LibraryIntrinsicCapability.ArrayInterfaceAdd, LibraryIntrinsicCapability.ArrayInterfaceSet })
        {
            var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != capability)] }] };
            var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
            using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
            var failed = ValueTranslator.TranslateIntegerObjects(denied, input, mutation, implementation);
            Assert.False(failed.Success); Assert.Contains(failed.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
    }

    [Fact]
    public async Task List_and_readonly_nominal_interfaces_and_admitted_members_match_reference()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var contract = resolver.ResolveAssembly(plan.Bindings[0].Contract.Assembly).Value!;
        using var sourceStream = File.OpenRead(LibraryBuildProducerTests.Request(files, CoreLibraryBuildTests.CorePath("CoreTypes.cs")).References.Single());
        using var targetStream = File.OpenRead(built.AssemblyPath);
        using var sourceImage = new PEReader(sourceStream); using var targetImage = new PEReader(targetStream);
        var sourceReader = sourceImage.GetMetadataReader(); var targetReader = targetImage.GetMetadataReader();
        foreach (var (ns, name) in new[] { ("System.Collections.Generic", "List`1"), ("System.Collections.ObjectModel", "ReadOnlyCollection`1") })
        {
            var source = resolver.ResolveType(contract, ns, name).Value!;
            var target = resolver.ResolveType(implementation, ns, name).Value!;
            var expected = sourceReader.GetTypeDefinition(source.Handle).GetInterfaceImplementations().Select(h =>
                resolver.ResolveLibrarySignatureType(plan, contract, sourceReader.GetInterfaceImplementation(h).Interface).Value).ToHashSet();
            var actual = targetReader.GetTypeDefinition(target.Handle).GetInterfaceImplementations().Select(h =>
                resolver.ResolveSignatureType(implementation, targetReader.GetInterfaceImplementation(h).Interface).Value).ToHashSet();
            Assert.DoesNotContain(null, expected); Assert.True(expected.SetEquals(actual));
            var names = name == "List`1" ? new[] { "get_Count", "get_Capacity", "set_Capacity", "get_Item", "set_Item", "Add", "AsReadOnly" } : new[] { "get_Count", "get_Item", "get_Items" };
            foreach (string member in names)
            {
                var method = Assert.Single(sourceReader.GetTypeDefinition(source.Handle).GetMethods(), h => sourceReader.GetString(sourceReader.GetMethodDefinition(h).Name) == member);
                var mapped = resolver.ResolveLibraryMethod(plan, contract, method);
                Assert.True(mapped.Success, string.Join("; ", mapped.Diagnostics));
            }
        }
    }
}
