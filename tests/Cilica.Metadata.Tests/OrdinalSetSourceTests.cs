using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class OrdinalSetSourceTests
{
    private static readonly string[] Names=["Cases","Identity","SameComparer","DifferentComparer","Null","Order","DeferredFault"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(OrdinalSetSourceProbe),Names));
    [Fact]
    public async Task Proven_ordinal_source_constructor_translates()=>Assert.Equal(Names.Length,(await Programs.Value).Length);
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    public async Task Source_construction_preserves_keys_order_identity_and_faults(bool optimized,int group)
    {
        var programs=await Programs.Value;
        var selected=group==3?new[]{6}:group==2?new[]{4}:new[]{group*2,group*2+1};
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,selected.Select(i=>(Names[i],programs[i],typeof(OrdinalSetSourceProbe).GetMethod(Names[i])!.CreateDelegate<Func<int,int,int>>())).ToArray(),expectedStaticRoots:1);
    }
    [Theory]
    [InlineData(false,false)] [InlineData(true,false)] [InlineData(false,true)] [InlineData(true,true)]
    public async Task Source_expression_fault_precedes_ordinal_getter(bool optimized,bool negative)
    {
        var pairs=(from a in new[]{0,1,-1,127,32768,int.MinValue,int.MaxValue} where (a<0)==negative from b in new[]{0,1,2,3,-1,int.MinValue,int.MaxValue} select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("Order",(await Programs.Value)[5],OrdinalSetSourceProbe.Order)],pairs,expectedStaticRoots:negative?0:1);
    }
    [Fact]
    public async Task Unproven_comparers_and_nonstring_owners_remain_diagnostic()
    {
        using var files=new ResolverFiles(); var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await System.IO.File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(OrdinalSetSourceProbe).Assembly.Location).Value!;
        foreach(var suffix in new[]{"Null","Alias","Custom","Getter","Join","Integer"}) {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(System.Reflection.Metadata.MethodDefinitionHandle)System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(typeof(OrdinalSetSourceProbe).GetMethod("Unsupported"+suffix)!.MetadataToken),implementation);
            Assert.False(result.Success,suffix);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("directly proven StringComparer.Ordinal",StringComparison.Ordinal));
        }
    }
}
