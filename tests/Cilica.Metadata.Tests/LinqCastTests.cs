using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;
public sealed class LinqCastTests
{
    private static readonly string[] Names = ["TypedShortcut", "References", "Scalars", "Nullable", "Values", "DeferredStates", "Faults", "CollectionTerminals", "CollectionStates", "CollectionFaults", "CollectionSizing", "Projection", "ReadOnlyDowncast", "NullAndMismatch", "CollectionCardinality", "ProjectionSizing", "EmptyArray"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(LinqCastProbe), Names));
    [Fact]
    public async Task Cast_probes_translate() => Assert.Equal(Names.Length, (await Programs.Value).Length);
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    [InlineData(false, 4)] [InlineData(true, 4)] [InlineData(false, 5)] [InlineData(true, 5)]
    public async Task Cast_iteration_and_terminal_behavior_matches_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(LinqCastProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Every_fault_mode_and_projection_size_matches_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        foreach (string name in new[] { "Faults", "CollectionFaults", "ProjectionSizing", "CollectionCardinality" })
        {
            int index = Array.IndexOf(Names, name);
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,
                [(name, programs[index], typeof(LinqCastProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())],
                pairs: (from input in new[] { -3, int.MaxValue } from mode in Enumerable.Range(0, 8) select (input, mode)).ToArray());
        }
    }

}
