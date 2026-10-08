using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class RangeTests
{
    [Theory]
    [InlineData(false,"Values")] [InlineData(true,"Values")]
    [InlineData(false,"Edges")] [InlineData(true,"Edges")]
    [InlineData(false,"Validation")] [InlineData(true,"Validation")]
    [InlineData(false,"Cursor")] [InlineData(true,"Cursor")]
    [InlineData(false,"List")] [InlineData(true,"List")]
    [InlineData(false,"Copy")] [InlineData(true,"Copy")]
    [InlineData(false,"Terminals")] [InlineData(true,"Terminals")]
    [InlineData(false,"Selection")] [InlineData(true,"Selection")]
    [InlineData(false,"SelectionFaults")] [InlineData(true,"SelectionFaults")]
    [InlineData(false,"SourceCursor")] [InlineData(true,"SourceCursor")]
    [InlineData(false,"Skip")] [InlineData(true,"Skip")]
    [InlineData(false,"Empty")] [InlineData(true,"Empty")]
    public async Task Range_bounds_cursor_lists_and_query_paths_match_dotnet(bool optimized,string name)
    {
        var program=(await CoreLibraryBuildTests.TranslateFixtures(typeof(RangeProbe),[name]))[0];
        var pairs=(from a in new[]{int.MinValue,-129,-1,0,1,127,int.MaxValue}from b in new[]{-1,0,1,2,7,16,int.MaxValue}select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(name,program,typeof(RangeProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())],pairs,expectedStaticRoots:name is "Skip" or "Empty"?1:0);
    }
}
