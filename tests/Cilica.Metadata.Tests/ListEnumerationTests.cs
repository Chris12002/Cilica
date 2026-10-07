using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ListEnumerationTests
{
    private static readonly string[] Names = ["Integers", "Empty", "Copy", "Mutation", "NoMutation", "References", "Values", "Default", "EndMutation", "BreakAndFinally", "AllocatingValues", "NestedOwner"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ListEnumerationProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Concrete_enumeration_matches_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ListEnumerationProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 4).Take(4).ToArray());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Legacy_current_on_a_boxed_concrete_cursor_matches_dotnet(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(ListEnumerationProbe), ["UnsupportedLegacyCurrent"]))[0];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("LegacyCurrent", program, ListEnumerationProbe.UnsupportedLegacyCurrent)]);
    }
}
