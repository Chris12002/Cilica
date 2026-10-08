using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class CandidateStringTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateStringProbe), "NullOrEmpty", "Evaluation"));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Null_empty_utf16_and_argument_evaluation_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [
            ("NullOrEmpty", programs[0], CandidateStringProbe.NullOrEmpty),
            ("Evaluation", programs[1], CandidateStringProbe.Evaluation)]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Independent_edit_distance_requirement_probe_matches_dotnet(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateStringProbe), "EditDistance"))[0];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("Distance", program, CandidateStringProbe.EditDistance)],
            (from a in Enumerable.Range(0, 8) from b in Enumerable.Range(0, 8) select (a, b)).ToArray());
    }

    [Fact]
    public void Independent_oracle_has_known_edit_distance_results()
    {
        Assert.Equal(3, CandidateStringProbe.EditDistance(1, 2));
        Assert.Equal(6, CandidateStringProbe.EditDistance(0, 1));
        Assert.Equal(2, CandidateStringProbe.EditDistance(3, 4));
        Assert.Equal(1, CandidateStringProbe.EditDistance(5, 6));
        Assert.Equal(0, CandidateStringProbe.EditDistance(7, 7));
    }
}
