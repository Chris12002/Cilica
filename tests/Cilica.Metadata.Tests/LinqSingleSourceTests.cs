using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class LinqSingleSourceTests
{
    private static readonly string[] Names=["Arrays","Generic","Lists","CollectionOnly","Nullable","References","Values","Composition","ReadOnly"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(LinqSingleSourceProbe),Names));
    [Fact]
    public async Task Source_only_single_probes_translate() => Assert.Equal(Names.Length,(await Programs.Value).Length);
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    [InlineData(false,3)] [InlineData(true,3)] [InlineData(false,4)] [InlineData(true,4)]
    public async Task Single_cardinality_callbacks_and_faults_match_dotnet(bool optimized,int group)
    {
        var programs=await Programs.Value;
        foreach(int i in Enumerable.Range(group*2,Math.Min(2,Names.Length-group*2)))
        {
            (int,int)[]? pairs=Names[i] switch {
                "Generic"=>(from a in Enumerable.Range(0,8) from b in Enumerable.Range(0,5) select(a,b)).ToArray(),
                "Lists"=>(from a in new[]{-1,0,1,2,3,int.MinValue,int.MaxValue} from b in new[]{0,1,2} select(a,b)).ToArray(),
                _=>null
            };
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,
                [(Names[i],programs[i],typeof(LinqSingleSourceProbe).GetMethod(Names[i])!.CreateDelegate<Func<int,int,int>>())],pairs);
        }
    }
}
