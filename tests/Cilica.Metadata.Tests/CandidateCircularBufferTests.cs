using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class CandidateCircularBufferTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Reduced_copy_and_clear_match_dotnet(bool optimized)
    {
        var programs=await CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateCircularBufferProbe),["WholeCopy","OverlapCopy","SectionClear","SegmentView"]);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("WholeCopy",programs[0],CandidateCircularBufferProbe.WholeCopy),("OverlapCopy",programs[1],CandidateCircularBufferProbe.OverlapCopy),("SectionClear",programs[2],CandidateCircularBufferProbe.SectionClear),("SegmentView",programs[3],CandidateCircularBufferProbe.SegmentView)]);
    }
    [Fact]
    public void Reduced_dotnet_oracles_cover_overlap_clear_and_live_segment_aliasing()
    {
        Assert.Equal(69,CandidateCircularBufferProbe.WholeCopy(2,7));
        Assert.Equal(128,CandidateCircularBufferProbe.OverlapCopy(2,7));
        Assert.Equal(85,CandidateCircularBufferProbe.SectionClear(2,7));
        Assert.Equal(250,CandidateCircularBufferProbe.SegmentView(2,7));
    }
}
