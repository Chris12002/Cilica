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
public sealed class StringArrayConcatTests
{
    private static readonly string[] Names = ["Text", "SingleIdentity", "MultiIdentity", "EmptyAndNull", "Repeated", "NativeMessage"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(StringArrayConcatProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task String_array_concatenation_matches_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(StringArrayConcatProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Oversized_total_throws_before_allocating_the_result(bool optimized)
    {
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(StringArrayConcatProbe), ["TooLong"]);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("TooLong", programs[0], StringArrayConcatProbe.TooLong)], [(0, 0)]);
    }
    [Fact]
    public async Task Array_concatenation_requires_utf16_construction_and_rejects_object_overload()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts); var grant = Assert.Single(artifact.Grants.Where(g => g.Capability == LibraryIntrinsicCapability.StringFromCodeUnits));
        var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(manifest with { Artifacts = [artifact with { Grants = artifact.Grants.Remove(grant) }] });
        var input = resolver.OpenInput(typeof(StringArrayConcatProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(StringArrayConcatProbe).GetMethod(name)!.MetadataToken);
        using (var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!)
        {
            var result = ValueTranslator.TranslateIntegerObjects(denied, input, Method("Text"), implementation);
            Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        Assert.False(ValueTranslator.TranslateIntegerObjects(scope, input, Method("UnsupportedObjects"), implementation).Success);
    }
}
