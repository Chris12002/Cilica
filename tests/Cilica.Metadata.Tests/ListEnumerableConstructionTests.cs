using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ListEnumerableConstructionTests
{
    private static readonly string[] Names = ["Integers", "Faults", "References", "Values", "Null", "Yield", "RuntimeCollection"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ListEnumerableConstructionProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)] [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Enumerable_construction_matches_dotnet(bool optimized, int batch)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ListEnumerableConstructionProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(batch * 3).Take(3).ToArray());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Every_fault_stage_preserves_disposal_order(bool optimized)
    {
        var program = (await Programs.Value)[1];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("FaultStages", program, ListEnumerableConstructionProbe.Faults)],
            pairs: (from index in Enumerable.Range(0, 4) from mode in Enumerable.Range(0, 7) select (index, mode)).ToArray());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Dynamic_collection_count_and_copy_failures_keep_the_fast_path(bool optimized)
    {
        var program = (await Programs.Value)[6];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("CollectionStages", program, ListEnumerableConstructionProbe.RuntimeCollection)],
            pairs: (from mode in Enumerable.Range(0, 5) from value in new[] { -3, 17 } select (mode, value)).ToArray());
    }

}
