using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class LinqDirectFirstTests
{
    private static readonly string[] Names = ["Arrays", "Null", "Lists", "Indexed", "Fallback", "Collection", "References", "Values", "Distinct", "WhereSelect", "ExistingCursor", "Concat", "Ordered", "OfType"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(LinqDirectFirstProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    [InlineData(false, 4)] [InlineData(true, 4)] [InlineData(false, 5)] [InlineData(true, 5)]
    [InlineData(false, 6)] [InlineData(true, 6)]
    public async Task First_without_predicate_matches_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        var pairs = (from a in new[] { int.MinValue, -7, -1, 0, 1, 7, int.MaxValue }
                     from b in new[] { -7, -6, -5, -4, -3, -2, -1, 0, 1, 2, 3, 4, 5, 6, 7 } select (a,b)).ToArray();
        var probes = Names.Select((name, i) =>
            (name, programs[i], typeof(LinqDirectFirstProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group * 2).Take(2).ToArray();
        if (group == 6)
        {
            // Ordered retains precisely the default Int32 comparer singleton.
            await IntegerObjectTranslationTests.VerifyTranslated(optimized, [probes[0]], pairs, expectedStaticRoots: 1);
            await IntegerObjectTranslationTests.VerifyTranslated(optimized, [probes[1]], pairs);
        }
        else await IntegerObjectTranslationTests.VerifyTranslated(optimized, probes, pairs);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task List_first_captures_storage_and_preserves_loaded_values(bool optimized)
    {
        string[] names = ["ListTerminals", "ListReferences"];
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(LinqDirectFirstProbe), names);
        var pairs = (from a in Enumerable.Range(0, 9) from b in Enumerable.Range(-4, 9) select (a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, names.Select((name, i) =>
            (name, programs[i], typeof(LinqDirectFirstProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).ToArray(), pairs);
    }
}
