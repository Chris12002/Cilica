using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class HiddenValueGenericTests
{
    private static readonly string[] Names=["Copy","Delegate","Constructed","PrivateNullable","PrivateBox","PublicFields","ReadonlyDelegate"];
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)]
    public async Task Hidden_value_arguments_preserve_copies_boxes_and_traced_fields(bool optimized,int group)
    {
        var names=Names.Skip(group*3).Take(3).ToArray();var programs=await CoreLibraryBuildTests.TranslateFixtures(typeof(HiddenValueGenericProbe),names);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,names.Select((name,i)=>(name,programs[i],typeof(HiddenValueGenericProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).ToArray());
    }
}
