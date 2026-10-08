using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
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
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Previously_rejected_comparer_construction_matches_dotnet(bool optimized)
    {
        string[] names=["UnsupportedNull","UnsupportedAlias","UnsupportedCustom","UnsupportedGetter","UnsupportedJoin","UnsupportedInteger"];
        var programs=await CoreLibraryBuildTests.TranslateFixtures(typeof(OrdinalDictionaryProbe),names);
        var pairs=(from a in new[]{1,7} from b in new[]{-1,0,1,7} select(a,b)).ToArray();
        for(int i=0;i<names.Length;i++)
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(names[i],programs[i],
                typeof(OrdinalDictionaryProbe).GetMethod(names[i])!.CreateDelegate<Func<int,int,int>>())],pairs,
                expectedStaticRoots:i==1||i==3||i==4?1:0);
    }
    [Fact]
    public async Task Managed_constructors_need_no_ordinal_construction_grants()
    {
        using var files=new ResolverFiles();
        var (built,_,resolver,_,_)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),
            new JsonSerializerOptions{Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        Assert.DoesNotContain(Assert.Single(manifest.Artifacts).Grants,g=>g.Capability==LibraryIntrinsicCapability.DictionaryOrdinalConstruction);
    }
}
