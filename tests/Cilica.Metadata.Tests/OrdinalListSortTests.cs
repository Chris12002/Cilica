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
public sealed class OrdinalListSortTests
{
    private static readonly string[] Names = ["Cases", "Version", "Null", "Identity"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(OrdinalListSortProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    public async Task Exact_ordinal_order_utf16_identity_versions_and_null_faults_match_dotnet(bool optimized,int group)
    {
        var programs = await Programs.Value;
        var probes = Names.Select((name,i)=>(name,programs[i],typeof(OrdinalListSortProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>()));
        if (group==0) await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes.Take(1).ToArray(),
            (from length in new[]{0,1,2,3,15,16,17,31,32,63,64,79} from pattern in new[]{0,1,2,3} select(length,pattern)).ToArray(),expectedStaticRoots:1);
        else await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes.Skip(1).ToArray(),expectedStaticRoots:1);
    }
    [Fact]
    public async Task Null_alias_custom_getter_join_and_nonstring_comparers_remain_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(OrdinalListSortProbe).Assembly.Location).Value!;
        foreach (string suffix in new[]{"Null","Alias","Custom","Getter","Join","Integer"}) {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(OrdinalListSortProbe).GetMethod("Unsupported"+suffix)!.MetadataToken),implementation);
            Assert.False(result.Success,suffix); Assert.Contains(result.Diagnostics,d=>d.Message.Contains("directly proven StringComparer.Ordinal",StringComparison.Ordinal));
        }
    }
    [Fact]
    public async Task Ordinal_bridge_requires_its_exact_grant()
    {
        using var files=new ResolverFiles();var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),
            new JsonSerializerOptions{Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var artifact=Assert.Single(manifest.Artifacts);var grant=Assert.Single(artifact.Grants.Where(g=>g.Capability==LibraryIntrinsicCapability.ListOrdinalSort));
        var bytes=AssemblyResolver.SerializeLibraryBuildManifest(manifest with {Artifacts=[artifact with {Grants=artifact.Grants.Remove(grant)}]});
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,Convert.ToHexStringLower(SHA256.HashData(bytes))).Value!;
        var input=scope.OpenInput(typeof(OrdinalListSortProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(OrdinalListSortProbe).GetMethod("Cases")!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);
    }
}
