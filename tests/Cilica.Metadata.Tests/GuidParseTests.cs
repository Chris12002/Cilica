using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class GuidParseTests
{
    private static readonly string[] Names=["RoundTrip","Repeated","Storage","Whitespace","Mutation","Corpus"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(GuidParseProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    public async Task Static_parse_matches_pinned_formats_legacy_inputs_and_failures(bool optimized,int group)
    {
        var programs=await Programs.Value;
        var probes=Names.Select((name,i)=>(name,programs[i],typeof(GuidParseProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>()));
        int[] units=[0,9,13,32,0x85,0xa0,0x1680,0x2000,0x200a,0x2028,0x2029,0x202f,0x205f,0x3000,0x200b,0xfeff,0xd800,0xffff,43,48,70,120];
        var pairs=group==0?null:group==1?units.Select(a=>(a,17)).ToArray():group==2?
            (from a in Enumerable.Range(0,70) from b in new[]{0,32,43,45,48,65,70,97,102,120,0xd800,0xffff} select(a,b)).ToArray():
            (from a in Enumerable.Range(0,51) from b in new[]{0,1,-1} select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,(group==0?probes.Take(3):probes.Skip(group+2).Take(1)).ToArray(),pairs);
    }
}
