using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class GroupByTests
{
    private static readonly string[] Names=["Stable","Resize","Wide","ValuesAsKeys","Strings","Repeated","Terminals","Cursor","GroupCursor","GroupList","SourceFault","SelectorRetry","MutableKeys","Empty","Validation","InlineElements"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(GroupByProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    [InlineData(false,4)] [InlineData(true,4)] [InlineData(false,5)] [InlineData(true,5)]
    [InlineData(false,6)] [InlineData(true,6)] [InlineData(false,7)] [InlineData(true,7)]
    [InlineData(false,8)] [InlineData(true,8)] [InlineData(false,9)] [InlineData(true,9)]
    [InlineData(false,10)] [InlineData(true,10)] [InlineData(false,11)] [InlineData(true,11)]
    [InlineData(false,12)] [InlineData(true,12)] [InlineData(false,13)] [InlineData(true,13)]
    [InlineData(false,14)] [InlineData(true,14)]
    [InlineData(false,15)] [InlineData(true,15)]
    public async Task Default_grouping_preserves_keys_elements_callbacks_and_cursor_state(bool optimized,int index)
    {
        var program=(await Programs.Value)[index];var pairs=(from a in new[]{-129,-1,0,1,2,7,127}from b in new[]{-1,0,1,2,3,4,5,6,7}select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(Names[index],program,typeof(GroupByProbe).GetMethod(Names[index])!.CreateDelegate<Func<int,int,int>>())],pairs,expectedStaticRoots:index is 13 or 14?2:0);
    }
}
