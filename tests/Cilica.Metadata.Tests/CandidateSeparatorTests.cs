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
public sealed class CandidateSeparatorTests
{
    private static readonly string[] Names = ["Payload", "WhitespaceUnits", "Repeated", "FaultOrder"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateSeparatorProbe), Names));

    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    [InlineData(false, 3)] [InlineData(true, 3)]
    public async Task Ordered_separator_arrays_text_identity_and_faults_match_dotnet(bool optimized, int probe)
    {
        var programs = await Programs.Value;
        (int, int)[] pairs = probe switch {
            0 => (from packed in Enumerable.Range(0, 18 * 4) from query in Enumerable.Range(-1, 50) select (packed, query)).ToArray(),
            1 => Enumerable.Range(0, 2048).Select(i => (i * 32, 0)).ToArray(),
            2 => (from a in Enumerable.Range(0, 4) from flags in Enumerable.Range(0, 4) select (a, flags)).ToArray(),
            _ => (from mode in Enumerable.Range(0, 5) from flags in new[] { int.MinValue, -1, 0, 1, 2, 3, 4, 8, int.MaxValue } select (mode, flags)).ToArray()
        };
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [(Names[probe], programs[probe], typeof(CandidateSeparatorProbe).GetMethod(Names[probe])!.CreateDelegate<Func<int, int, int>>())], pairs);
    }

    [Theory]
    [InlineData("UnsupportedCount")] [InlineData("UnsupportedSingle")]
    public async Task Count_and_single_string_overloads_remain_diagnostic(string name)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(CandidateSeparatorProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CandidateSeparatorProbe).GetMethod(name)!.MetadataToken);
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Id == "CIL3010" && d.Message.Contains("Split", StringComparison.Ordinal));
    }
}
