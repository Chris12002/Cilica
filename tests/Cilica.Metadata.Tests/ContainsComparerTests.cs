using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ContainsComparerTests
{
    private static readonly string[] Names=["Strings","CollectionDefaults","CustomCollection","Traversal","Comparer","Grow","ArrayMutation","Values","Wide","Null"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(ContainsComparerProbe),Names));
    [Fact]
    public async Task Explicit_comparer_search_validates_at_both_stages()
    {foreach(var p in await Programs.Value){ManagedExceptionIrValidator.Validate(p);ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(p));}}
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)] [InlineData(false,4)] [InlineData(true,4)]
    public async Task Explicit_equality_storage_mutation_and_disposal_match_dotnet(bool optimized,int group)
    {
        var programs=await Programs.Value;
        var pairs=(from a in Enumerable.Range(0,32) from b in new[]{0,1,2,3,-1,int.MaxValue} select(a,b)).ToArray();
        foreach(int i in Enumerable.Range(group*2,2))
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(Names[i],programs[i],typeof(ContainsComparerProbe).GetMethod(Names[i])!.CreateDelegate<Func<int,int,int>>())],pairs,expectedStaticRoots:i<2?2:0);
    }
    [Fact]
    public async Task Unsupported_default_key_shapes_remain_diagnostic()
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(ContainsComparerProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ContainsComparerProbe).GetMethod("UnsupportedNullable")!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("Nullable",StringComparison.Ordinal));
        result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ContainsComparerProbe).GetMethod("UnsupportedIgnoreCaseSet")!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("directly proven StringComparer.Ordinal",StringComparison.Ordinal));
    }
}
