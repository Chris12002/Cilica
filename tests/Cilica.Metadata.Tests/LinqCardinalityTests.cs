using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class LinqCardinalityTests
{
    private static readonly string[] Names = ["Arrays", "References", "Values", "GenericCollection", "LegacyCollection", "Fallback", "Faults", "Null"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(LinqCardinalityProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Count_shortcuts_enumeration_and_disposal_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        var inputs = new[] { int.MinValue, -3, -1, 0, 1, 2, 3, int.MaxValue };
        var pairs = (from a in inputs from b in new[] { -8, -7, -6, -5, -4, -3, -2, -1, 0, 1, 2, 3, 4, 5, 6, 7 } select (a, b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(LinqCardinalityProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray(), pairs);
    }
    [Fact]
    public async Task Unimplemented_overloads_and_boxed_collection_factories_remain_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(LinqCardinalityProbe).Assembly.Location).Value!;
        foreach (var (name, message) in new[] { ("UnsupportedList", "no implemented managed body"), ("UnsupportedIndexedWhere", "No implementation method matches") })
        {
            var handle = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(LinqCardinalityProbe).GetMethod(name)!.MetadataToken);
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, handle, implementation);
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(message, StringComparison.Ordinal));
        }
    }
}
