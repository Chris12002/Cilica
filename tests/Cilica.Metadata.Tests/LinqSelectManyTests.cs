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
public sealed class LinqSelectManyTests
{
    private static readonly string[] Names=["Values","Deferred","Cursors","Validation","Faults","NullInner","Terminals","MaterializeArray","MaterializeList","MaterializeFault","ArraysAndLists","Payloads","Composition"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(LinqSelectManyProbe),Names));
    [Fact]
    public async Task SelectMany_probes_translate() => Assert.Equal(Names.Length,(await Programs.Value).Length);
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    [InlineData(false,3)] [InlineData(true,3)] [InlineData(false,4)] [InlineData(true,4)] [InlineData(false,5)] [InlineData(true,5)] [InlineData(false,6)] [InlineData(true,6)]
    public async Task Deferred_flattening_callbacks_and_cleanup_match_dotnet(bool optimized,int group)
    {
        var programs=await Programs.Value;
        foreach(int i in Enumerable.Range(group*2,Math.Min(2,Names.Length-group*2)))
        {
            (int,int)[]? pairs=null;
            if(Names[i]=="Faults")pairs=(from a in new[]{-1,0,17} from b in Enumerable.Range(0,11) select(a,b)).ToArray();
            if(Names[i] is "MaterializeArray" or "MaterializeList")
            { int[] sizes=[0,1,7,8,9,15,16,17,23,24,25,31,32,33];pairs=(from a in sizes from b in sizes select(a,b)).ToArray(); }
            pairs ??= (from a in new[]{0,1,-1,127,32768,int.MinValue,int.MaxValue}
                       from b in new[]{0,1,2,3,-1,int.MinValue,int.MaxValue} select(a,b)).ToArray();
            foreach (var groupPairs in pairs.GroupBy(pair=>StaticRoots(Names[i],pair.Item1,pair.Item2)))
                await IntegerObjectTranslationTests.VerifyTranslated(optimized,
                    [(Names[i],programs[i],typeof(LinqSelectManyProbe).GetMethod(Names[i])!.CreateDelegate<Func<int,int,int>>())],
                    groupPairs.ToArray(),expectedStaticRoots:groupPairs.Key);
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Checked_counts_and_legacy_cursor_contracts_match_dotnet(bool optimized)
    {
        var programs=await CoreLibraryBuildTests.TranslateFixtures(typeof(LinqSelectManyProbe),"OverflowCount","LegacyCursor");
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("OverflowCount",programs[0],LinqSelectManyProbe.OverflowCount)]);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("LegacyCursor",programs[1],LinqSelectManyProbe.LegacyCursor)],expectedStaticRoots:2);
    }
    // C# caches the noncapturing selector delegates and their shared target.
    // Empty array/list enumeration adds its one shared persistent cursor.
    // Keep exact per-input root counts rather than accepting arbitrary retention.
    private static int StaticRoots(string name,int a,int b) => name switch {
        "Values"=>3,
        "Cursors" or "Validation" or "NullInner" or "Payloads"=>2,
        "MaterializeArray" or "MaterializeList"=>(a&63)==0 && (b&63)==0 ? 1 : 0,
        "ArraysAndLists"=>3+(a==0 && b==0 ? 1 : 0),
        "Composition"=>4+((a&7)==0 && (b&7)==0 ? 1 : 0),
        _=>0
    };
    [Fact]
    public async Task Other_SelectMany_overloads_remain_diagnostic()
    {
        using var files=new ResolverFiles();var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(LinqSelectManyProbe).Assembly.Location).Value!;
        foreach(string name in new[]{"UnsupportedIndexed","UnsupportedResult","UnsupportedNestedVector"})
        {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(LinqSelectManyProbe).GetMethod(name)!.MetadataToken),implementation);
            Assert.False(result.Success,name);Assert.Null(result.Program);Assert.Contains(result.Diagnostics,d=>d.Message.Contains(name=="UnsupportedNestedVector"?"Nested vectors":"SelectMany",StringComparison.Ordinal));
        }
    }
}
