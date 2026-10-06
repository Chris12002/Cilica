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
public sealed class LinqConcatTests
{
    private static readonly string[] Names = ["Arrays", "EmptyAndNull", "Cursor", "Faults", "BothCollections", "FirstCollection", "SecondCollection", "NeitherCollection", "CollectionChain", "MixedChain", "Cardinality", "ChainCardinality", "Preservation", "Projection", "References", "Values", "Nullable", "EarlyBreak", "ZeroProjection", "ZeroCollections", "MixedValues"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(LinqConcatProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    [InlineData(false, 4)] [InlineData(true, 4)] [InlineData(false, 5)] [InlineData(true, 5)]
    public async Task Concat_cursor_callbacks_and_terminals_match_dotnet(bool optimized, int group)
        => await Verify(optimized, group);
    [Theory]
    [InlineData(false, 1)] [InlineData(true, 1)] [InlineData(false, 6)] [InlineData(true, 6)]
    public async Task Concat_collection_faults_empty_projection_and_segmented_values_match_dotnet(bool optimized, int group)
        => await Verify(optimized, group);
    private static async Task Verify(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(LinqConcatProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray(),
            (from a in new[] { 0, 1, -1, 127, 32768, int.MinValue, int.MaxValue }
             from b in new[] { 0, 1, 2, 3, -1, int.MinValue, int.MaxValue } select (a, b))
            .Concat(from a in Enumerable.Range(0, 9) from b in Enumerable.Range(0, 9) select (a, b)).Distinct().ToArray());
    }
    [Fact]
    public async Task Boxed_list_cursor_factory_remains_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(LinqConcatProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(LinqConcatProbe).GetMethod("UnsupportedList")!.MetadataToken);
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("no implemented managed body", StringComparison.Ordinal) && d.Location!.Contains("GetEnumerator", StringComparison.Ordinal));
    }

}
