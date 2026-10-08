using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using System.Threading.Tasks;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class NullableTextTests
{
    [Theory]
    [InlineData(false,"Named")] [InlineData(true,"Named")]
    [InlineData(false,"Mutable")] [InlineData(true,"Mutable")]
    [InlineData(false,"EmptyMutable")] [InlineData(true,"EmptyMutable")]
    [InlineData(false,"Heap")] [InlineData(true,"Heap")]
    [InlineData(false,"Array")] [InlineData(true,"Array")]
    [InlineData(false,"TextComposite")] [InlineData(true,"TextComposite")]
    [InlineData(false,"Copies")] [InlineData(true,"Copies")]
    public async Task Nullable_text_preserves_absence_mutation_copies_and_faults(bool optimized,string name)
    {
        var program=(await CoreLibraryBuildTests.TranslateFixtures(typeof(NullableTextProbe),[name]))[0];
        var pairs=(from a in new[]{int.MinValue,-129,-1,0,1,127,int.MaxValue}from b in new[]{-1,0,1,2,3,4,5}select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(name,program,typeof(NullableTextProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())],pairs);
    }
    [Theory]
    [InlineData("Integer", "primitive fallbacks")]
    [InlineData("Wide", "primitive fallbacks")]
    [InlineData("Boolean", "Narrow field addresses")]
    [InlineData("Character", "Narrow field addresses")]
    [InlineData("UnsupportedNullableComposite", "directly proven box")]
    public async Task Separate_unimplemented_lowerings_remain_diagnostic(string name,string message)
    {
        using var files=new ResolverFiles();var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=resolver.OpenInput(typeof(NullableTextProbe).Assembly.Location).Value!;var method=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(NullableTextProbe).GetMethod(name)!.MetadataToken);
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,method,implementation);Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains(message,StringComparison.Ordinal));
    }

}
