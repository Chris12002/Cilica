using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class LinqMaterializationTests
{
    private static readonly string[] Names = ["Arrays", "EmptyAndNull", "Collections", "Values", "EmptyValues", "Covariant", "Growth", "WhereCursor", "WhereIdentity", "Fallback", "WhereFaults", "References", "ListEnumerable"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(LinqMaterializationProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    [InlineData(false, 4)] [InlineData(true, 4)]
    public async Task Array_materialization_matches_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(LinqMaterializationProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Previously_rejected_unsigned_snapshot_matches_dotnet(bool optimized)
    {
        // UInt32 vectors were admitted before grouping; keep this former gap as
        // an execution control rather than expecting a now-stale diagnostic.
        const string name = "UnsupportedUnsigned";
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(LinqMaterializationProbe), [name]))[0];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [(name, program, LinqMaterializationProbe.UnsupportedUnsigned)]);
    }
}
