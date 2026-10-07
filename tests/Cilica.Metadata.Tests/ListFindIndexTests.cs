using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ListFindIndexTests
{
    private static readonly string[] Names = ["Bounds", "Empty", "Integers", "References", "Values"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ListFindIndexProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    [InlineData(false, 3)] [InlineData(true, 3)]
    [InlineData(false, 4)] [InlineData(true, 4)]
    public async Task Predicate_search_matches_dotnet(bool optimized, int probe)
    {
        int[] first = probe < 2 ? [-1,0,1,3,4,5,int.MinValue,int.MaxValue] : Enumerable.Range(0, probe == 2 ? 9 : 7).ToArray();
        int[] second = probe < 2 ? [-1,0,1,2,4,5,int.MinValue,int.MaxValue] : [-1,0,1,3,5,7,73,79,83,89];
        var p = (await Programs.Value)[probe];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [(Names[probe], p, typeof(ListFindIndexProbe).GetMethod(Names[probe])!.CreateDelegate<Func<int,int,int>>())],
            (from a in first from b in second select (a,b)).ToArray());
    }
}
