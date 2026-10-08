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
public sealed class CandidateTrimSetTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateTrimSetProbe), "Single", "Set", "Whitespace", "Evaluation", "Mutation"));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Character_and_set_directions_payload_identity_and_faults_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        var pairs = (from scenario in Enumerable.Range(0, 36) from index in Enumerable.Range(-3, 20) select (scenario,index)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("TrimSingle", programs[0], CandidateTrimSetProbe.Single), ("TrimSet", programs[1], CandidateTrimSetProbe.Set),
             ("TrimWhitespace", programs[2], CandidateTrimSetProbe.Whitespace)], pairs);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("TrimEvaluation", programs[3], CandidateTrimSetProbe.Evaluation)], Enumerable.Range(0,9).Select(x => (x,0)).ToArray());
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("TrimMutation", programs[4], CandidateTrimSetProbe.Mutation)],
            (from scenario in Enumerable.Range(0,12) from index in Enumerable.Range(-3,20) select (scenario,index)).ToArray());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Previously_missing_character_array_reduction_matches_dotnet(bool optimized)
    {
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateTrimSetProbe), "MissingCharacters");
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("FormerCharacterArrayGap", programs[0], CandidateTrimSetProbe.MissingCharacters)]);
    }
}
