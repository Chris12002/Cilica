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
public sealed class CandidateCircularBufferTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Reduced_copy_and_clear_match_dotnet(bool optimized)
    {
        var programs=await CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateCircularBufferProbe),["WholeCopy","OverlapCopy","SectionClear"]);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("WholeCopy",programs[0],CandidateCircularBufferProbe.WholeCopy),("OverlapCopy",programs[1],CandidateCircularBufferProbe.OverlapCopy),("SectionClear",programs[2],CandidateCircularBufferProbe.SectionClear)]);
    }
    [Fact]
    public async Task Array_segment_requirement_remains_an_actionable_gap()
    {
        using var files=new ResolverFiles();
        var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var contract=resolver.ResolveAssembly(plan.Bindings.First().Contract.Assembly).Value!;
        var segment=resolver.ResolveType(contract,"System","ArraySegment`1").Value!;
        string segmentToken=$"0x{MetadataTokens.GetToken(segment.Handle):x8}";
        var input=scope.OpenInput(typeof(CandidateCircularBufferProbe).Assembly.Location).Value!;
        foreach(var(name,requirement)in new[]{("SegmentView","ArraySegment")})
        {
            var method=typeof(CandidateCircularBufferProbe).GetMethod(name)!;
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(method.MetadataToken),implementation);
            output.WriteLine(name+": "+string.Join("; ",result.Diagnostics));
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics,d=>name=="SegmentView"
                ? d.Id=="CIL3015"&&d.Message.Contains(segmentToken,StringComparison.Ordinal)
                : d.Id=="CIL3010"&&d.Message.Contains(requirement,StringComparison.Ordinal));
        }
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
