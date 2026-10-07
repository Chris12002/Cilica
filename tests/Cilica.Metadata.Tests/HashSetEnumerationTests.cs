using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class HashSetEnumerationTests
{
    private static readonly string[] Names = ["Concrete", "Mutation", "Interface", "CurrentAndReset", "Values", "Default", "Empty"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(HashSetEnumerationProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    public async Task Set_cursors_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        var pairs = (from a in new[] { int.MinValue, -1, 0, 1, 7, int.MaxValue }
                     from b in Enumerable.Range(0, 8) select (a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(HashSetEnumerationProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group * 2).Take(2).ToArray(), pairs,
            expectedStaticRoots: group == 3 ? 1 : 0);
    }
}
