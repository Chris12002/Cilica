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
public sealed class DictionaryDefaultTests
{
    private static readonly string[] Names=["Integers","Wide","Values","References","Strings","Callbacks","CallbackDefaults","Null"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(DictionaryDefaultProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    [InlineData(false,4)] [InlineData(true,4)] [InlineData(false,5)] [InlineData(true,5)]
    [InlineData(false,6)] [InlineData(true,6)] [InlineData(false,7)] [InlineData(true,7)]
    public async Task Dictionary_defaults_and_callback_behavior_match_dotnet(bool optimized,int index)
    {
        var program=(await Programs.Value)[index];var pairs=(from a in new[]{int.MinValue,-129,-1,0,1,127,int.MaxValue}from b in new[]{-7,-1,0,1,2,7,127}select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(Names[index],program,typeof(DictionaryDefaultProbe).GetMethod(Names[index])!.CreateDelegate<Func<int,int,int>>())],pairs);
    }
    [Theory]
    [InlineData("UnsupportedTryAdd")] [InlineData("UnsupportedRemove")]
    public async Task Other_collection_extensions_remain_diagnostic(string name)
    {
        using var files=new ResolverFiles();var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=resolver.OpenInput(typeof(DictionaryDefaultProbe).Assembly.Location).Value!;var method=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(DictionaryDefaultProbe).GetMethod(name)!.MetadataToken);
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,method,implementation);Assert.False(result.Success);Assert.NotEmpty(result.Diagnostics);
    }
}
