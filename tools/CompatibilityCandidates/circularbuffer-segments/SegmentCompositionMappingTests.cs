using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class SegmentCompositionMappingTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Record_owned_composition_admission_without_executing_upstream()
    {
        Assert.Equal(211, SegmentCompositionProbe.SegmentList(2,7));
        Assert.Equal(38, SegmentCompositionProbe.CopyViews(2,7));
        Assert.Equal(492, SegmentCompositionProbe.IteratorViews(2,7));
        Assert.Equal(14, SegmentCompositionProbe.DirectPropertyCopy(2,7));
        Assert.Equal(14, SegmentCompositionProbe.LocalPropertyCopy(2,7));
        Assert.Equal(394, SegmentCompositionProbe.CursorViews(2,7));
        using var files = new ResolverFiles();
        var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(SegmentCompositionProbe).Assembly.Location).Value!;
        foreach(string name in new[]{"SegmentList","CopyViews","DirectPropertyCopy","LocalPropertyCopy","IteratorViews","CursorViews"})
        {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(SegmentCompositionProbe).GetMethod(name)!.MetadataToken),implementation);
            output.WriteLine(name+": success="+result.Success+"; "+string.Join("; ",result.Diagnostics));
        }
    }
}
