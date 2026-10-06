using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class NumericAppendTests
{
    private static readonly string[] Names = ["Signed32", "Unsigned32", "Signed64", "Unsigned64", "Chunks", "NullOrder"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(NumericAppendProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Numeric_append_matches_the_invariant_oracle(bool optimized, int group)
    {
        var programs = await Programs.Value;
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
                (name, programs[i], typeof(NumericAppendProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }
}
