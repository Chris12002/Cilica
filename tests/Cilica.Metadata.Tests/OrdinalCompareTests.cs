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
public sealed class OrdinalCompareTests
{
    private static readonly string[] Names=["Values","Units","Evaluation","Identity"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(OrdinalCompareProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    public async Task Exact_ordinal_comparison_results_utf16_and_evaluation_match_dotnet(bool optimized,int group)
    {
        var programs=await Programs.Value;
        int[] units=[0,1,65,127,255,0x7fff,0x8000,0xd7ff,0xd800,0xdbff,0xdc00,0xdfff,0xe000,0xfffe,0xffff];
        var pairs=group==0?(from a in Enumerable.Range(0,20) from b in Enumerable.Range(0,20) select(a,b)).ToArray()
            :group==1?(from a in units from b in units select(a,b)).ToArray():null;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,Names.Select((name,i)=>(name,programs[i],typeof(OrdinalCompareProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group).Take(group==2?2:1).ToArray(),pairs);
    }
    [Fact]
    public async Task Modes_aliases_and_exact_grant_are_checked_even_for_null_arguments()
    {
        using var files=new ResolverFiles();var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        var bytes=await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken);
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(OrdinalCompareProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name)=>(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(OrdinalCompareProbe).GetMethod(name)!.MetadataToken);
        foreach(string suffix in new[]{"Culture","IgnoreCase","Unknown","Parameter","Alias"}) {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,Method("Unsupported"+suffix),implementation);
            Assert.False(result.Success,suffix);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("directly proven StringComparison.Ordinal",StringComparison.Ordinal));
        }
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(bytes,new JsonSerializerOptions{Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var artifact=Assert.Single(manifest.Artifacts);var grant=Assert.Single(artifact.Grants.Where(g=>g.Capability==LibraryIntrinsicCapability.StringOrdinalCompare));
        var deniedBytes=AssemblyResolver.SerializeLibraryBuildManifest(manifest with {Artifacts=[artifact with {Grants=artifact.Grants.Remove(grant)}]});
        using var denied=resolver.CreateTrustedLibraryCompilationScope(plan,deniedBytes,Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
        var rejected=ValueTranslator.TranslateIntegerObjects(denied,input,Method("Values"),implementation);
        Assert.False(rejected.Success);Assert.Contains(rejected.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);
    }
}
