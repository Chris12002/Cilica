using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Linq;
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
public sealed class OrdinalStringTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(OrdinalStringProbe), "Equality"));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Direct_ordinal_utf16_comparison_and_nulls_match_dotnet(bool optimized) =>
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("Ordinal", (await Programs.Value)[0], OrdinalStringProbe.Equality)]);
    [Fact]
    public async Task Enum_contract_exact_grant_and_constant_proof_are_required()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(OrdinalStringProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(OrdinalStringProbe).GetMethod(name)!.MetadataToken);
        foreach (string name in new[] { "IgnoreCase", "Culture", "Unknown", "Alias" })
        {
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, Method(name), implementation);
            Assert.False(result.Success); Assert.Null(result.Program);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains("directly proven StringComparison.Ordinal", StringComparison.Ordinal));
        }
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions {
            Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != LibraryIntrinsicCapability.StringOrdinalEqual)] }] };
        var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
        using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
        var rejected = ValueTranslator.TranslateIntegerObjects(denied, input, Method("Equality"), implementation);
        Assert.False(rejected.Success); Assert.Contains(rejected.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        using var image = new PEReader(File.OpenRead(built.AssemblyPath));
        var reader = image.GetMetadataReader();
        var type = resolver.ResolveType(implementation, "System", "StringComparison").Value!;
        var constants = reader.GetTypeDefinition(type.Handle).GetFields().Select(reader.GetFieldDefinition)
            .Where(f => !f.GetDefaultValue().IsNil).ToDictionary(f => reader.GetString(f.Name), f =>
                reader.GetBlobReader(reader.GetConstant(f.GetDefaultValue()).Value).ReadInt32());
        Assert.Equal(6, constants.Count);
        foreach (var value in Enum.GetValues<StringComparison>()) Assert.Equal((int)value, constants[value.ToString()]);
    }
}
