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
public sealed class LinqSkipTests
{
    private static readonly string[] Names=["Arrays","Generic","Collection","CustomList","Mutation","Identity","Repeated","Interfaces","CopyFailures","References","Values","Null","MutableEquality","AllocatingEquality","Covariance","NullCopy","Legacy"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(LinqSkipProbe),Names));
    [Fact]
    public async Task Supported_sources_translate() => Assert.Equal(Names.Length,(await Programs.Value).Length);
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    [InlineData(false,4)] [InlineData(true,4)] [InlineData(false,5)] [InlineData(true,5)]
    [InlineData(false,6)] [InlineData(true,6)] [InlineData(false,7)] [InlineData(true,7)]
    [InlineData(false,8)] [InlineData(true,8)] [InlineData(false,9)] [InlineData(true,9)]
    [InlineData(false,10)] [InlineData(true,10)] [InlineData(false,11)] [InlineData(true,11)]
    [InlineData(false,12)] [InlineData(true,12)] [InlineData(false,13)] [InlineData(true,13)]
    [InlineData(false,14)] [InlineData(true,14)]
    [InlineData(false,15)] [InlineData(true,15)] [InlineData(false,16)] [InlineData(true,16)]
    public async Task Deferred_sources_lists_terminals_and_faults_match_dotnet(bool optimized,int probe)
    {
        var pairs=(from a in new[]{int.MinValue,-1,0,1,2,3,4,5,int.MaxValue}
                   from b in new[]{-1,0,1,2,3,4,int.MinValue,int.MaxValue} select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [(Names[probe],(await Programs.Value)[probe],typeof(LinqSkipProbe).GetMethod(Names[probe])!.CreateDelegate<Func<int,int,int>>())],pairs, expectedStaticRoots: probe is 0 or 1 or 2 or 3 or 6 ? 1 : 0);
    }
    [Theory]
    [InlineData("UnsupportedWhere")] [InlineData("UnsupportedSelect")] [InlineData("UnsupportedOrdered")]
    [InlineData("UnsupportedConcat")] [InlineData("UnsupportedDistinct")] [InlineData("UnsupportedOfType")]
    [InlineData("UnsupportedCast")] [InlineData("UnsupportedMany")]
    public async Task Unimplemented_optimized_sources_are_build_diagnostics(string name)
    {
        using var files=new ResolverFiles();
        var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(LinqSkipProbe).Assembly.Location).Value!;
        var token=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(LinqSkipProbe).GetMethod(name)!.MetadataToken);
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,token,implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("no implemented managed body",StringComparison.Ordinal));
    }
}
