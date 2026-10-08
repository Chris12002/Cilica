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
public sealed class NullableFormattingTests
{
    private static readonly string[] Names=["Named","Mutation","Faults","Two","Three","GenericCall","Enum","MixedEnum"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(NullableFormattingProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    [InlineData(false,4)] [InlineData(true,4)] [InlineData(false,5)] [InlineData(true,5)]
    [InlineData(false,6)] [InlineData(true,6)] [InlineData(false,7)] [InlineData(true,7)]
    public async Task Nullable_boxes_format_their_payload_and_preserve_absence(bool optimized,int index)
    {
        var program=(await Programs.Value)[index];var pairs=(from a in new[]{int.MinValue,-129,-1,0,1,127,int.MaxValue}from b in new[]{-7,-1,0,1,2,7,127}select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(Names[index],program,typeof(NullableFormattingProbe).GetMethod(Names[index])!.CreateDelegate<Func<int,int,int>>())],pairs);
    }
    [Theory]
    [InlineData("UnsupportedAlias")] [InlineData("UnsupportedJoin")] [InlineData("UnsupportedMixedAlias")]
    [InlineData("UnsupportedCustom")] [InlineData("UnsupportedPrimitive")]
    public async Task Aliases_joins_custom_and_primitive_nullable_formats_remain_diagnostic(string name)
    {
        using var files=new ResolverFiles();var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=resolver.OpenInput(typeof(NullableFormattingProbe).Assembly.Location).Value!;var method=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(NullableFormattingProbe).GetMethod(name)!.MetadataToken);
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,method,implementation);Assert.False(result.Success);Assert.NotEmpty(result.Diagnostics);
    }
}
