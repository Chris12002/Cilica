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
public sealed class OrdinalDictionaryTests
{
    private static readonly string[] Names = ["Cases", "Identity", "Values", "Version"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(OrdinalDictionaryProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    public async Task Exact_ordinal_order_utf16_identity_versions_and_null_faults_match_dotnet(bool optimized,int group)
    {
        var programs = await Programs.Value;
        var probes = Names.Select((name,i)=>(name,programs[i],typeof(OrdinalDictionaryProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>()));
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes.Skip(group*2).Take(2).ToArray(),expectedStaticRoots:1);
    }
    [Fact]
    public async Task Null_alias_custom_getter_join_and_nonstring_comparers_remain_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(OrdinalDictionaryProbe).Assembly.Location).Value!;
        foreach (string suffix in new[]{"Null","Alias","Custom","Getter","Join","Integer"}) {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(OrdinalDictionaryProbe).GetMethod("Unsupported"+suffix)!.MetadataToken),implementation);
            Assert.False(result.Success,suffix); Assert.Contains(result.Diagnostics,d=>d.Message.Contains("directly proven StringComparer.Ordinal",StringComparison.Ordinal));
        }
    }
    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task Ordinal_bridge_requires_its_exact_grant(int parameterCount)
    {
        using var files=new ResolverFiles();var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),
            new JsonSerializerOptions{Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var artifact=Assert.Single(manifest.Artifacts);var grant=Assert.Single(artifact.Grants.Where(g=>g.Capability==LibraryIntrinsicCapability.DictionaryOrdinalConstruction && resolver.ReadMethodDeclarationInput(implementation,(MethodDefinitionHandle)MetadataTokens.EntityHandle(g.Token),new()).Value!.Signature.Parameters.Count==parameterCount));
        var bytes=AssemblyResolver.SerializeLibraryBuildManifest(manifest with {Artifacts=[artifact with {Grants=artifact.Grants.Remove(grant)}]});
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,Convert.ToHexStringLower(SHA256.HashData(bytes))).Value!;
        var input=scope.OpenInput(typeof(OrdinalDictionaryProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle((parameterCount==1?typeof(OrdinalDictionaryProbe):typeof(OrdinalDictionaryCapacityProbe)).GetMethod("Cases")!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);
    }
}
