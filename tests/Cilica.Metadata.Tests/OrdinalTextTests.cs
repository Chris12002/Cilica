using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class OrdinalTextTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(OrdinalTextProbe), "Compare", "Empty", "Units", "Identity"));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Null_prefix_padding_and_utf16_order_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        var pairs = (from first in Enumerable.Range(0, 20) from second in Enumerable.Range(0, 20) select (first, second)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("Compare", programs[0], OrdinalTextProbe.Compare), ("Empty", programs[1], OrdinalTextProbe.Empty)], pairs);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Constructed_strings_compare_exact_code_units(bool optimized)
    {
        var programs = await Programs.Value;
        int[] units = [0, 1, 65, 127, 255, 0x7fff, 0x8000, 0xd7ff, 0xd800, 0xdbff, 0xdc00, 0xdfff, 0xe000, 0xfffe, 0xffff];
        var pairs = (from first in units from second in units select (first, second)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("Units", programs[2], OrdinalTextProbe.Units), ("Identity", programs[3], OrdinalTextProbe.Identity)], pairs);
    }
}
