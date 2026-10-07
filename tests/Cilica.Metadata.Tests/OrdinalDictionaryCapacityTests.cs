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
public sealed class OrdinalDictionaryCapacityTests
{
    private static readonly string[] Names = ["Cases", "Identity", "Values", "Version", "Negative", "Order"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(OrdinalDictionaryCapacityProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    public async Task Exact_ordinal_order_utf16_identity_versions_and_null_faults_match_dotnet(bool optimized,int group)
    {
        var programs = await Programs.Value;
        var probes = Names.Select((name,i)=>(name,programs[i],typeof(OrdinalDictionaryCapacityProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>()));
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes.Skip(group*2).Take(group==2?1:2).ToArray(),expectedStaticRoots:1);
    }
    [Theory]
    [InlineData(false,false)] [InlineData(true,false)] [InlineData(false,true)] [InlineData(true,true)]
    public async Task Capacity_argument_fault_precedes_getter_cache_allocation(bool optimized,bool negative)
    {
        var program=(await Programs.Value)[5];
        var pairs=(from a in new[]{0,1,-1,127,32768,int.MinValue,int.MaxValue}
            where (a<0)==negative
            from b in new[]{0,1,2,3,-1,int.MinValue,int.MaxValue} select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("Order",program,OrdinalDictionaryCapacityProbe.Order)],pairs,expectedStaticRoots:negative?0:1);
    }
    [Fact]
    public async Task Null_alias_custom_getter_join_and_nonstring_comparers_remain_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(OrdinalDictionaryCapacityProbe).Assembly.Location).Value!;
        foreach (string suffix in new[]{"Null","Alias","Custom","Getter","Join","Integer"}) {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(OrdinalDictionaryCapacityProbe).GetMethod("Unsupported"+suffix)!.MetadataToken),implementation);
            Assert.False(result.Success,suffix); Assert.Contains(result.Diagnostics,d=>d.Message.Contains("directly proven StringComparer.Ordinal",StringComparison.Ordinal));
        }
    }
}
