using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ReferenceArrayBorrowTests
{
    private static readonly string[] Names=["Cache","ReplaceArray","Reborrow","Strings","Generic","Faults"];
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    public async Task Array_slot_aliases_roots_generics_and_covariance_faults_match_dotnet(bool optimized,int group)
    {
        var names=Names.Skip(group*2).Take(2).ToArray();
        var programs=await CoreLibraryBuildTests.TranslateFixtures(typeof(ReferenceArrayBorrowProbe),names);
        foreach(var program in programs)
            Assert.Contains(program.Flow.Flow.Flow.Methods.SelectMany(m=>m.Blocks).SelectMany(b=>b.Values),
                v=>v.Expression is IrExpression.AddressElement { ArrayType.ArrayElement.Kind:ObjectArrayElementKind.Reference });
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,names.Select((name,i)=>
            (name,programs[i],typeof(ReferenceArrayBorrowProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).ToArray());
    }
}
