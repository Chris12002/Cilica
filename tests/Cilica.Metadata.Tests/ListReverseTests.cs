using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ListReverseTests
{
    private static readonly string[] Names=["Whole","Range","Faults","Version","References","Values","WideNullable","Query","NullReceiver"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(ListReverseProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    [InlineData(false,4)] [InlineData(true,4)] [InlineData(false,5)] [InlineData(true,5)]
    [InlineData(false,6)] [InlineData(true,6)] [InlineData(false,7)] [InlineData(true,7)]
    [InlineData(false,8)] [InlineData(true,8)]
    public async Task Reversal_ranges_faults_and_version_match_dotnet(bool optimized,int i)
    {
        var programs=await Programs.Value;
        var boundaries=new[]{int.MinValue,-1,0,1,2,3,4,5,6,int.MaxValue};
        (int,int)[]? pairs=Names[i]=="Faults"?(from a in boundaries from b in boundaries select(a,b)).ToArray():null;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(Names[i],programs[i],typeof(ListReverseProbe).GetMethod(Names[i])!.CreateDelegate<Func<int,int,int>>())],pairs,expectedStaticRoots:Names[i]=="Query"?2:0);
    }
}
