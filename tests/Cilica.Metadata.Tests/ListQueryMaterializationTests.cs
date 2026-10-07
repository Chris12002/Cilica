using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ListQueryMaterializationTests
{
    private static readonly string[] Names = ["Integers", "References", "Values", "OriginalCursor", "CorruptCount"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ListQueryMaterializationProbe), Names));
    [Theory]
    [InlineData(false, 0, 9, 6)] [InlineData(true, 0, 9, 6)]
    [InlineData(false, 1, 7, 4)] [InlineData(true, 1, 7, 4)]
    [InlineData(false, 2, 7, 4)] [InlineData(true, 2, 7, 4)]
    [InlineData(false, 3, 9, 2)] [InlineData(true, 3, 9, 2)]
    [InlineData(false, 4, 1, 5)] [InlineData(true, 4, 1, 5)]
    public async Task Captured_list_materialization_matches_dotnet(bool optimized, int probe, int modes, int kinds)
    {
        var program = (await Programs.Value)[probe];
        var oracle = typeof(ListQueryMaterializationProbe).GetMethod(Names[probe])!.CreateDelegate<Func<int, int, int>>();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [(Names[probe], program, oracle)],
            pairs: (from mode in Enumerable.Range(0, modes) from kind in Enumerable.Range(0, kinds) select (mode, kind)).ToArray());
    }
}
