using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class StackEnumerationTests
{
    private static readonly string[] Names=["Concrete","Copies","Interfaces","Generic","Values","OfType","Mutation","EmptyConcrete","EmptyInterfaces","DefaultCursor"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(StackEnumerationProbe),Names));
    [Fact]
    public async Task Stack_cursor_composition_validates_at_both_stages()
    {foreach(var p in await Programs.Value){ManagedExceptionIrValidator.Validate(p);ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(p));}}
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)] [InlineData(false,4)] [InlineData(true,4)]
    public async Task Stack_cursors_match_dotnet_with_collection_and_frame_limits(bool optimized,int group)
    {
        var programs=await Programs.Value;
        foreach(var item in Names.Select((name,i)=>(Name:name,Program:programs[i],Oracle:typeof(StackEnumerationProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2))
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,[item],expectedStaticRoots:item.Name=="EmptyInterfaces"?1:0);
    }
}
