using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class PrimitiveReferenceTypeTestTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Closed_primitive_reference_tests_preserve_null_casts_and_identity(bool optimized)
    {
        string[] names = ["String", "Object"];
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(PrimitiveReferenceTypeTestProbe), names);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, names.Select((name, i) =>
            (name, programs[i], typeof(PrimitiveReferenceTypeTestProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).ToArray());
    }
}
