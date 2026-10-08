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
public sealed class InvariantUpperTests
{
    private static int PinnedMap(int scalar) => scalar == 0x017f ? 0x0053 : Cilica.Runtime.UnicodeOrdinalUpper.Map(scalar);
    private static readonly string[] Names = ["Scalar", "Block", "Identity", "Mixed", "Null"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(InvariantUpperProbe), Names));

    [Fact]
    public void Pinned_lookup_matches_every_scalar_and_lone_surrogate_on_oracle_host()
    {
        for (int scalar = 0; scalar <= 0x10ffff; ++scalar)
        {
            string source = scalar <= 0xffff ? new string((char)scalar, 1) : char.ConvertFromUtf32(scalar);
            string expected = source.ToUpperInvariant();
            int mapped = PinnedMap(scalar);
            string actual = mapped <= 0xffff ? new string((char)mapped, 1) : char.ConvertFromUtf32(mapped);
            Assert.True(expected == actual, $"U+{scalar:X}: pinned mapping U+{mapped:X} differs from this host's globalization data");
            Assert.Equal(source.Length, actual.Length);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Every_changed_scalar_and_range_neighbors_match_native_exactly(bool optimized)
    {
        var points = Enumerable.Range(0, 0x110000)
            .Where(c => PinnedMap(c) != c)
            .SelectMany(c => new[] { c - 1, c, c + 1 })
            .Concat(new[] { 0, 127, 128, 0x130, 0x131, 0x17f, 0xd800, 0xdbff, 0xdc00, 0xdfff, 0xffff, 0x10ffff })
            .Distinct().Order().Select(c => (c, 0)).ToArray();
        foreach (var batch in points.Chunk(192))
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,
                [("UpperScalar", (await Programs.Value)[0], InvariantUpperProbe.Scalar)], batch);
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 2)] [InlineData(false, 3)]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 2)] [InlineData(true, 3)]
    public async Task Entire_unicode_range_in_native_strings_matches_dotnet_blocks(bool optimized, int part)
    {
        // 544 blocks cover every code point; supplementary blocks use valid pairs.
        var pairs = Enumerable.Range(part * 136, 136).Select(i => (i * 2048, 2048)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("UpperBlock", (await Programs.Value)[1], InvariantUpperProbe.Block)], pairs);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Identity_mixed_surrogates_nulls_and_retained_source_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Skip(2).Select((name, i) =>
            (name, programs[i + 2], typeof(InvariantUpperProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).ToArray(),
            pairs: (from a in new[] { -1, 0, 1, 2, 3, 4, 5, 6, 7, 0x130, 0xd800, 0xdc00, 0xffff, int.MinValue, int.MaxValue }
                    from b in new[] { 0, 31, 0x3a3, 0xd800, 0xdfff, int.MinValue, int.MaxValue } select (a, b)).ToArray());
    }

    [Fact]
    public async Task Culture_and_current_culture_overloads_remain_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(InvariantUpperProbe).Assembly.Location).Value!;
        foreach (string name in new[] { "UnsupportedCulture", "UnsupportedCurrent" })
        {
            var token = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(InvariantUpperProbe).GetMethod(name)!.MetadataToken);
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, token, implementation);
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains("No implementation method matches", StringComparison.Ordinal));
        }
    }
}
