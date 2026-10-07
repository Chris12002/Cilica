using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ListQueryAdmissionTests
{
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    public async Task Earlier_list_diagnostics_now_have_native_evidence(bool optimized,int probe)
    {
        Type[] types=[typeof(LinqCardinalityProbe),typeof(LinqDistinctProbe),typeof(LinqSingleProbe),typeof(LinqOfTypeProbe)];
        string[] names=["ListCardinality","ListDistinct","ListSingle","ListOfType"];
        var p=(await CoreLibraryBuildTests.TranslateFixtures(types[probe],names[probe]))[0];
        var item=(names[probe],p,types[probe].GetMethod(names[probe])!.CreateDelegate<Func<int,int,int>>());
        if(probe!=2) { await IntegerObjectTranslationTests.VerifyTranslated(optimized,[item],expectedStaticRoots:probe==0?0:1);return; }
        var inputs=new[]{0,1,2,3,-1,int.MinValue,int.MaxValue};
        foreach(bool empty in new[]{false,true})
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,[item],
                (from a in inputs where ((a&3)==0)==empty from b in inputs select(a,b)).ToArray(),expectedStaticRoots:empty?1:0);
    }
}
