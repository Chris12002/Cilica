using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class LinqToListTests
{
    private static readonly string[] Names = ["Basic", "Empty", "FallbackFaults", "Collection", "Concat", "Queries", "NegativeListCount", "ReportedQueryCount", "OfTypeReferences"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(LinqToListProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    [InlineData(false, 3)] [InlineData(true, 3)]
    [InlineData(false, 4)] [InlineData(true, 4)]
    [InlineData(false, 6)] [InlineData(true, 6)]
    [InlineData(false, 7)] [InlineData(true, 7)]
    [InlineData(false, 8)] [InlineData(true, 8)]
    public async Task List_materialization_matches_dotnet(bool optimized, int probe)
    {
        int[] first = probe switch { 0 => [0,1,2,3], 1 => [0,1], 2 => [0,1,2,3,4,5,6], 3 => [0,1,2,3,4], 4 => [0,1,2,3,4,5,6,7], 5 => [0,1,2,3,4,5,6], 6 => [-2,-1,0,1,3], 7 => [-2,-1,0,1,2,4,6], _ => [0,1,2,3,8,17] };
        int[] second = probe switch { 0 or 5 => [0,1,2,3,8,17], 1 or 3 or 8 => [0], 4 => [0,1,2,3,4,5,6,7], _ => [0,1,2,3] };
        var p = (await Programs.Value)[probe];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [(Names[probe], p, typeof(LinqToListProbe).GetMethod(Names[probe])!.CreateDelegate<Func<int,int,int>>())],
            (from a in first from b in second select (a,b)).ToArray());
    }
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task Query_results_have_exact_capacity_and_cache_roots(bool optimized, bool ordered)
    {
        var p = (await Programs.Value)[5];
        int[] kinds = ordered ? [4,6] : [0,1,2,3,5];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("Queries", p, LinqToListProbe.Queries)],
            (from kind in kinds from count in new[] { 0,1,2,3,8,17 } select (kind,count)).ToArray(),
            expectedStaticRoots: ordered ? 1 : 0); // One per-instance Comparer<int>.Default singleton.
    }

}
