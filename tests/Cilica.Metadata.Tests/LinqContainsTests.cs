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
public sealed class LinqContainsTests
{
    private static readonly string[] Names = ["Integers", "Strings", "Null", "Source", "SourceFault", "CollectionEnumerable", "Concat", "Ordered", "Distinct", "ProjectionCursor", "GeneralProjectionCursor", "ActiveProjection", "ListProjection", "OfType", "OfTypeValueProjection", "MutatingValues", "AllocatingValues"];
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    [InlineData(false, 4)] [InlineData(true, 4)] [InlineData(false, 5)] [InlineData(true, 5)]
    public async Task Contains_preserves_equality_callbacks_cursor_state_and_disposal(bool optimized, int group)
    {
        string[] names = Names.Skip(group * 3).Take(3).ToArray();
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(LinqContainsProbe), names);
        // OrderBy eagerly retains one instance-owned default comparer even when
        // Contains bypasses sorting. Keep that exact root separate from no-cache probes.
        foreach (var subset in names.Select((name, i) =>
            (Name: name, Program: programs[i], Oracle: typeof(LinqContainsProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>()))
            .GroupBy(probe => probe.Name == "Ordered"))
            await IntegerObjectTranslationTests.VerifyTranslated(optimized, subset.ToArray(),
                (from a in new[] { int.MinValue, -3, -1, 0, 1, 2, 3, 4, 7, int.MaxValue }
                 from b in new[] { int.MinValue, -1, 0, 1, 2, 3, 4, 7, 10, 11, 12, int.MaxValue } select (a, b)).ToArray(),
                persistentRoots: group >= 3, expectedStaticRoots: subset.Key ? 1 : 0);
    }
    [Theory]
    [InlineData("UnsupportedNullable", "Nullable")]
    public async Task Unimplemented_overload_and_equality_shapes_remain_diagnostic(string name, string diagnostic)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(LinqContainsProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(LinqContainsProbe).GetMethod(name)!.MetadataToken);
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains(diagnostic, StringComparison.Ordinal));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Former_explicit_comparer_gap_executes(bool optimized)
    {
        var program=(await CoreLibraryBuildTests.TranslateFixtures(typeof(LinqContainsProbe),["UnsupportedComparer"]))[0];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("UnsupportedComparer",program,LinqContainsProbe.UnsupportedComparer)],expectedStaticRoots:1);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Former_reference_projection_gap_executes_with_exact_cache_roots(bool optimized)
    {
        var program=(await CoreLibraryBuildTests.TranslateFixtures(typeof(LinqContainsProbe),["UnsupportedReferenceProjection"]))[0];
        var pairs=(from a in new[]{int.MinValue,-3,-1,0,1,2,3,4,7,int.MaxValue}
                   from b in new[]{int.MinValue,-1,0,1,2,3,4,7,10,11,12,int.MaxValue} select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("UnsupportedReferenceProjection",program,LinqContainsProbe.UnsupportedReferenceProjection)],pairs,expectedStaticRoots:2);
    }

}
