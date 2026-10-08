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
public sealed class OrdinalOrderingTests
{
    private static readonly string[] Names=["Ascending","Descending","Chain","StringChain","Repeated","Terminals","PrimaryFirst","SourceFault","SelectorFault","Cursor","Validation","ThenValidation","EmptySingle"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(OrdinalOrderingProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    [InlineData(false,4)] [InlineData(true,4)] [InlineData(false,5)] [InlineData(true,5)]
    [InlineData(false,6)] [InlineData(true,6)] [InlineData(false,7)] [InlineData(true,7)]
    [InlineData(false,8)] [InlineData(true,8)] [InlineData(false,9)] [InlineData(true,9)]
    [InlineData(false,10)] [InlineData(true,10)] [InlineData(false,11)] [InlineData(true,11)]
    public Task Explicit_ordinal_ordering_preserves_callbacks_stability_and_faults(bool optimized,int index) => Verify(optimized,index);
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Empty_single_ordinal_ordering_preserves_terminal_shortcuts(bool optimized) => Verify(optimized,12);
    private static async Task Verify(bool optimized,int index)
    {
        var program=(await Programs.Value)[index];
        var pairs=(from a in new[]{int.MinValue,-1,0,1,2,7,int.MaxValue} from b in new[]{-1,0,1,2,3,7,15}select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(Names[index],program,typeof(OrdinalOrderingProbe).GetMethod(Names[index])!.CreateDelegate<Func<int,int,int>>())],pairs,expectedStaticRoots:index is 2 or 3 or 4 or 5 or 8 or 11 or 12?2:1);
    }
    [Theory]
    [InlineData("Aliased","directly proven StringComparer.Ordinal")]
    [InlineData("NullComparer","directly proven StringComparer.Ordinal")]
    [InlineData("CustomSource","custom or covariant IOrderedEnumerable")]
    [InlineData("CovariantSource","custom or covariant IOrderedEnumerable")]
    public async Task Unproven_comparers_and_custom_sources_stay_diagnostic(string name,string diagnostic)
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=resolver.OpenInput(typeof(OrdinalOrderingProbe).Assembly.Location).Value!;
        var handle=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(OrdinalOrderingProbe).GetMethod(name)!.MetadataToken);
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,handle,implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains(diagnostic,StringComparison.Ordinal));
    }
}
