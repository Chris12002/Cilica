using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class HashSetUnionTests
{
    private static readonly string[] Names=["Arrays","Wide","Values","Strings","Self","Interface","SourceFaults","CollectionIgnoreCount","Partial","NullOrder","ValueCallbacks","SelfCallbacks"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(HashSetUnionProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    [InlineData(false,4)] [InlineData(true,4)] [InlineData(false,5)] [InlineData(true,5)]
    [InlineData(false,6)] [InlineData(true,6)] [InlineData(false,7)] [InlineData(true,7)]
    [InlineData(false,8)] [InlineData(true,8)] [InlineData(false,9)] [InlineData(true,9)]
    [InlineData(false,10)] [InlineData(true,10)]
    [InlineData(false,11)] [InlineData(true,11)]
    public async Task Union_preserves_callbacks_partial_mutation_disposal_and_set_identity(bool optimized,int index)
    {
        var programs=await Programs.Value;
        var pairs=(from a in new[]{int.MinValue,-1,0,1,2,7,31,int.MaxValue} from b in new[]{-1,0,1,2,3,4,5,6,7} select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(Names[index],programs[index],typeof(HashSetUnionProbe).GetMethod(Names[index])!.CreateDelegate<Func<int,int,int>>())],pairs,expectedStaticRoots:index is 3 or 4 or 9?1:0);
    }
}
