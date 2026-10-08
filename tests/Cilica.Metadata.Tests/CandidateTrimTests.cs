using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class CandidateTrimTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateTrimProbe), "Units", "Text", "NullReceiver"));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Every_utf16_edge_unit_and_unchanged_identity_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("TrimUnits", programs[0], CandidateTrimProbe.Units)],
            Enumerable.Range(0, 2048).Select(i => (i * 32, 0)).ToArray());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Exact_payload_empty_identity_roots_and_receiver_faults_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("TrimText", programs[1], CandidateTrimProbe.Text)],
            (from a in Enumerable.Range(0, 8) from b in Enumerable.Range(-3, 16) select (a, b)).ToArray());
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("TrimNull", programs[2], CandidateTrimProbe.NullReceiver)]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Previously_diagnostic_character_and_start_reductions_match_dotnet(bool optimized)
    {
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateTrimProbe), "UnsupportedCharacters", "UnsupportedStart");
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("FormerCharacterGap", programs[0], CandidateTrimProbe.UnsupportedCharacters),
             ("FormerStartGap", programs[1], CandidateTrimProbe.UnsupportedStart)]);
    }
}
