using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
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
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Previously_rejected_comparer_construction_matches_dotnet(bool optimized)
    {
        string[] names=["UnsupportedNull","UnsupportedAlias","UnsupportedCustom","UnsupportedGetter","UnsupportedJoin","UnsupportedInteger"];
        var programs=await CoreLibraryBuildTests.TranslateFixtures(typeof(OrdinalDictionaryCapacityProbe),names);
        var pairs=(from a in new[]{1,7} from b in new[]{-1,0,1,7} select(a,b)).ToArray();
        for(int i=0;i<names.Length;i++)
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(names[i],programs[i],
                typeof(OrdinalDictionaryCapacityProbe).GetMethod(names[i])!.CreateDelegate<Func<int,int,int>>())],pairs,
                expectedStaticRoots:i==1||i==3||i==4?1:0);
    }
}
