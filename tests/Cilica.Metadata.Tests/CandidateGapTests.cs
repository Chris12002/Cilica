using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class CandidateGapTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task String_separator_reduction_now_matches_dotnet(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateGapProbe), "StringSeparators"))[0];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("StringSeparators", program, CandidateGapProbe.StringSeparators)], [(0, 0)]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Character_text_reduction_now_matches_dotnet(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateGapProbe), "CharacterText"))[0];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("CharacterText", program, CandidateGapProbe.CharacterText)],
            [(int.MinValue, 0), (-1, 0), (0, 0), (0xd800, 0), (0xffff, 0), (int.MaxValue, 0)]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Boolean_vector_reduction_now_matches_dotnet(bool optimized)
    {
        var program=(await CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateGapProbe),"BooleanVector"))[0];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("BooleanVector",program,CandidateGapProbe.BooleanVector)]);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Ignore_case_dictionary_reduction_now_matches_dotnet(bool optimized)
    {
        var program=(await CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateGapProbe),"IgnoreCaseComparer"))[0];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("IgnoreCaseComparer",program,CandidateGapProbe.IgnoreCaseComparer)],expectedStaticRoots:1);
    }
}
