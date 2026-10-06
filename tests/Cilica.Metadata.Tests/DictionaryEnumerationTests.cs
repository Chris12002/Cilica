using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class DictionaryEnumerationTests
{
    private static readonly string[] Names = ["Integers", "RemoveWhileIterating", "ClearWhileIterating", "OverwriteAndDuplicates", "AddedKeyFault", "CopiesAndEnd", "EmptyAndDefault", "ValuesAndAllocation", "References", "FinallyAndEndedAdd", "CurrentLifetime"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(DictionaryEnumerationProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Concrete_dictionary_enumeration_matches_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(DictionaryEnumerationProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 4).Take(4).ToArray());
    }
}
