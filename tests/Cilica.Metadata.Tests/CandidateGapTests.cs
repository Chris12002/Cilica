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
    [InlineData("BooleanVector", "CIL2001", "Composed vector elements")]
    [InlineData("CharacterText", "CIL3010", "ToString")]
    [InlineData("IgnoreCaseComparer", "CIL3010", "get_OrdinalIgnoreCase")]
    public async Task Remaining_candidate_requirements_are_explicit_admission_gaps(string name, string diagnosticId, string requirement)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(CandidateGapProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CandidateGapProbe).GetMethod(name)!.MetadataToken);
        var translated = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
        Assert.False(translated.Success);
        Assert.Null(translated.Program);
        Assert.Contains(translated.Diagnostics, d => d.Id == diagnosticId && d.Message.Contains(requirement, StringComparison.Ordinal));
        TestContext.Current.TestOutputHelper!.WriteLine(name + ": " + string.Join("; ", translated.Diagnostics));
    }
}
