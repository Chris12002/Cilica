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
public sealed class BooleanParseTests
{
    private static readonly string[] Names = ["Parse", "Parameter", "Twice", "ArgumentFault"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(BooleanParseProbe), Names));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Boolean_string_grammar_and_output_reset_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("Parse", programs[0], BooleanParseProbe.Parse), ("Parameter", programs[1], BooleanParseProbe.Parameter)],
            (from index in Enumerable.Range(0, 128) from initial in new[] { 0, 1 } select (index, initial)).ToArray());
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("Twice", programs[2], BooleanParseProbe.Twice), ("ArgumentFault", programs[3], BooleanParseProbe.ArgumentFault)]);
    }
    [Fact]
    public void Owned_parser_matches_every_UTF16_boundary_and_word_position()
    {
        static void Check(string? text)
        {
            bool expected = bool.TryParse(text, out bool expectedValue);
            bool actual = Runtime.BooleanParsing.TryParse(text!, out bool actualValue);
            Assert.Equal(expected, actual); Assert.Equal(expectedValue, actualValue);
        }
        for (int i = 0; i < 128; i++) Check(BooleanParseProbe.Input(i));
        foreach (string word in new[] { "true", "false" })
        {
            char[] chars = word.ToCharArray();
            for (int c = 0; c <= char.MaxValue; c++)
            {
                Check(((char)c) + word); Check(word + ((char)c));
                for (int i = 0; i < word.Length; i++)
                {
                    chars[i] = (char)c; Check(new string(chars)); chars[i] = word[i];
                }
            }
        }
    }
    [Theory]
    [InlineData("UnsupportedParse")] [InlineData("UnsupportedHeap")]
    public async Task Other_APIs_and_narrow_heap_addresses_remain_diagnostic(string name)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(BooleanParseProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(BooleanParseProbe).GetMethod(name)!.MetadataToken);
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains(name == "UnsupportedHeap" ? "storage-width-aware" : "Parse", StringComparison.Ordinal));
    }
}
