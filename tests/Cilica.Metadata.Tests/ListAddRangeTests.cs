using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ListAddRangeTests
{
    private static readonly string[] Names = ["Arrays", "Self", "WrappedSelf", "References", "Values", "Null", "CollectionCallbacks", "EnumerableFaults"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ListAddRangeProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Bulk_append_matches_dotnet(bool optimized, int batch)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ListAddRangeProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(batch * 3).Take(3).Where(p => p.name != "WrappedSelf").ToArray());
        if (batch == 0)
        {
            var pairs = (from input in new[] { 0, 1, -1, 127, 32768, int.MinValue, int.MaxValue }
                from mode in new[] { 0, 1, 2, 3, -1, int.MinValue, int.MaxValue } select (input, mode)).ToArray();
            await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("WrappedNonempty", programs[2], ListAddRangeProbe.WrappedSelf)],
                pairs: pairs.Where(p => (p.input & 3) != 0).ToArray());
            await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("WrappedEmpty", programs[2], ListAddRangeProbe.WrappedSelf)],
                pairs: pairs.Where(p => (p.input & 3) == 0).ToArray(), expectedStaticRoots: 1);
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Collection_callbacks_preserve_partial_stores_reentrancy_and_versions(bool optimized)
    {
        var program = (await Programs.Value)[6];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("CollectionCallbacks", program, ListAddRangeProbe.CollectionCallbacks)],
            pairs: (from mode in Enumerable.Range(0, 13) from value in new[] { -9, 17 } select (mode, value)).ToArray());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Enumerable_faults_preserve_partial_appends_and_disposal(bool optimized)
    {
        var program = (await Programs.Value)[7];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("EnumerableFaults", program, ListAddRangeProbe.EnumerableFaults)],
            pairs: (from index in Enumerable.Range(0, 4) from mode in Enumerable.Range(0, 7) select (index, mode)).ToArray());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Public_add_range_gap_matches_dotnet(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(CompatibilityGapProbe), ["Probe_ListAddRange"]))[0];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("PublicAddRange", program, CompatibilityGapProbe.Probe_ListAddRange)]);
    }
}
