using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class FixedStringConcatTests
{
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Fixed_string_overloads_preserve_utf16_shortcuts_identity_and_roots(bool optimized, int group)
    {
        string[] names = group == 0 ? ["Text3", "Text4", "Identity3"] : ["Identity4", "Repeated", "FaultMessage"];
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(FixedStringConcatProbe), names);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, names.Select((name, i) =>
            (name, programs[i], typeof(FixedStringConcatProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).ToArray(),
            (from a in Enumerable.Range(0, 81) from b in new[] { -1, 0, 1, 7 } select (a, b)).ToArray());
    }
}
